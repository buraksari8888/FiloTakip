import { Service, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../environments/environment';

// API'nin gönderdiği konum mesajı (API'deki LocationDto)
export interface LocationUpdate {
  locationId: number;
  vehicleId: number;
  latitude: number;
  longitude: number;
  speedKmh: number;
  fuelPercent: number | null;
  ignitionOn: boolean;
  recordedAt: string; // UTC
  roadSpeedLimitKmh: number | null; // bulunduğu yolun sınırı (API bulur)
}

// connecting: ilk bağlantı kuruluyor · connected: canlı · reconnecting: bağlantı yok, yeniden deneniyor
export type LiveStatus = 'connecting' | 'connected' | 'reconnecting';

// Bağlantı yoksa (ya da bir istek başarısız olursa) kaç milisaniyede bir yeniden denensin
export const RETRY_MS = 5000;

// API'nin canlı bildirim kanalına (SignalR, /hubs/live) tek bir bağlantı; bütün uygulama bunu paylaşır.
// API yeni konum ya da alarm değişikliği olunca buradan haber verir, sayfaların tekrar tekrar sorması gerekmez.
@Service()
export class LiveHub {
  private readonly locationAdded = new Subject<LocationUpdate>();
  private readonly alarmsChanged = new Subject<void>();
  private readonly vehiclesChanged = new Subject<void>();
  private readonly reconnected = new Subject<void>();
  private hadProblem = false; // bağlantı koptu mu ya da hiç kurulamadı mı?

  readonly status = signal<LiveStatus>('connecting');
  readonly locationAdded$ = this.locationAdded.asObservable();
  readonly alarmsChanged$ = this.alarmsChanged.asObservable();
  // Araç eklendi, pasife alındı ya da sürücüsü değişti (başka bir sekmede de olabilir)
  readonly vehiclesChanged$ = this.vehiclesChanged.asObservable();
  // Bağlantı koptuktan (ya da ilk denemede kurulamadıktan) sonra kurulunca. Aradaki mesajlar kaçmış olabilir,
  // bu yüzden veriyi gösteren servisler bunu duyunca listelerini baştan çeker.
  readonly reconnected$ = this.reconnected.asObservable();

  private readonly connection = new signalR.HubConnectionBuilder()
    // Çerez göndermeye gerek yok (API'nin CORS ayarı da çerezli isteklere izin vermiyor)
    .withUrl(environment.hubUrl, { withCredentials: false })
    // Bağlantı koparsa vazgeçmeden 5 saniyede bir yeniden dener
    .withAutomaticReconnect({ nextRetryDelayInMilliseconds: () => RETRY_MS })
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  constructor() {
    // Mesaj adları API'deki ILiveClient arayüzündeki metotlarla aynı
    this.connection.on('LocationAdded', (location: LocationUpdate) => this.locationAdded.next(location));
    this.connection.on('AlarmsChanged', () => this.alarmsChanged.next());
    this.connection.on('VehiclesChanged', () => this.vehiclesChanged.next());
    this.connection.onreconnecting(() => this.lost());
    this.connection.onreconnected(() => this.connected());
    void this.start();
  }

  private async start(): Promise<void> {
    try {
      await this.connection.start();
      this.connected();
    } catch {
      // İlk bağlantı kurulamadı (ör. API kapalı). Otomatik yeniden bağlanma sadece kurulmuş bir bağlantı
      // koparsa devreye girer, bu yüzden burada kendimiz yeniden deniyoruz.
      this.lost();
      setTimeout(() => void this.start(), RETRY_MS);
    }
  }

  private connected(): void {
    this.status.set('connected');
    if (this.hadProblem) {
      this.hadProblem = false;
      this.reconnected.next();
    }
  }

  private lost(): void {
    this.status.set('reconnecting');
    this.hadProblem = true;
  }
}
