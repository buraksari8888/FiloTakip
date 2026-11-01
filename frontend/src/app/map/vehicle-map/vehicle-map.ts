import { formatDate, formatNumber } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  afterNextRender,
  Component,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  LOCALE_ID,
  signal,
  viewChild,
} from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import * as L from 'leaflet';
import { catchError, filter, of, scan, startWith, switchMap } from 'rxjs';
import { Alarm } from '../../alarms/alarm';
import { AlarmApi } from '../../alarms/alarm-api';
import { LiveHub, LocationUpdate } from '../../core/live-hub';
import { Geofence, geofenceAlarmText } from '../../geofences/geofence';
import { GeofenceApi } from '../../geofences/geofence-api';
import { ApiErrorAlert } from '../../shared/api-error-alert/api-error-alert';
import { LiveVehicles } from '../../vehicles/live-vehicles';
import { VehicleApi } from '../../vehicles/vehicle-api';
import { isLowFuel, isSpeeding, VehicleLastLocation } from '../../vehicles/vehicle-last-location';
import { recentTrail, TRAIL_MINUTES, trailSegments } from './trail';

const ISTANBUL: L.LatLngTuple = [41.03, 28.98];
const FOCUS_ZOOM = 16; // tek bir araca ya da alarma yakınlaşırken (sokak düzeyi)
const GEOFENCE_PANE = 'geofences';

// Adresteki ?arac=3 / ?alarm=46 değerini sayıya çevirir; yoksa ya da geçersizse (ör. ?arac=abc) yok sayar
function toId(value: string | undefined): number | undefined {
  const id = Number(value);
  return Number.isInteger(id) && id > 0 ? id : undefined;
}

@Component({
  imports: [ApiErrorAlert],
  selector: 'app-vehicle-map',
  styleUrl: './vehicle-map.scss',
  templateUrl: './vehicle-map.html',
})
export class VehicleMap {
  private readonly live = inject(LiveVehicles);
  private readonly alarmApi = inject(AlarmApi);
  private readonly vehicleApi = inject(VehicleApi);
  private readonly geofenceApi = inject(GeofenceApi);
  private readonly hub = inject(LiveHub);
  private readonly locale = inject(LOCALE_ID);
  private readonly mapElement = viewChild.required<ElementRef<HTMLDivElement>>('map');

  // Listelerdeki "Haritada göster" bağlantılarından gelen adres parametreleri: /harita?arac=3 ya da
  // /harita?alarm=46. Router bunları doğrudan bu input'lara verir (app.config.ts: withComponentInputBinding).
  readonly vehicleId = input(undefined, { alias: 'arac', transform: toId });
  readonly alarmId = input(undefined, { alias: 'alarm', transform: toId });

  protected readonly apiError = this.live.apiError;
  protected readonly alarmError = signal<HttpErrorResponse | null>(null);

  // ?alarm=46 ise o alarm API'den çekilir (konumu, plakası, mesajı)
  private readonly alarm = toSignal(
    toObservable(this.alarmId).pipe(
      switchMap((alarmId) => {
        this.alarmError.set(null);
        if (alarmId === undefined) {
          return of(null);
        }
        return this.alarmApi.getAlarm(alarmId).pipe(
          catchError((error: HttpErrorResponse) => {
            this.alarmError.set(error);
            return of(null);
          }),
        );
      }),
    ),
    { initialValue: null },
  );

  // Bilgi balonu açık olan araç (yoksa null). İz sadece onun için çizilir: hepsinin izi birden haritayı karıştırırdı.
  private readonly selectedVehicleId = signal<number | null>(null);
  protected readonly trailError = signal<HttpErrorResponse | null>(null);

  // Seçili aracın izi: önce son TRAIL_MINUTES dakika API'den çekilir, sonra canlı gelen her konum ucuna eklenir.
  // Canlı bağlantı koptuktan sonra yeniden kurulunca iz baştan çekilir (kopukken gelen konumlar kaçmış olabilir).
  private readonly trail = toSignal(
    toObservable(this.selectedVehicleId).pipe(
      switchMap((vehicleId) => {
        this.trailError.set(null);
        if (vehicleId === null) {
          return of([]);
        }
        return this.hub.reconnected$.pipe(
          startWith(undefined),
          switchMap(() =>
            this.vehicleApi.getRecentLocations(vehicleId, TRAIL_MINUTES).pipe(
              catchError((error: HttpErrorResponse) => {
                this.trailError.set(error);
                return of([]);
              }),
            ),
          ),
          switchMap((history) =>
            this.hub.locationAdded$.pipe(
              filter((location) => location.vehicleId === vehicleId),
              scan((points, location) => recentTrail([...points, location], TRAIL_MINUTES), history),
              startWith(history),
            ),
          ),
        );
      }),
    ),
    { initialValue: [] },
  );

  protected readonly geofenceError = signal<HttpErrorResponse | null>(null);

  // Bölgeler sayfa açılınca bir kez çekilir (sık değişmezler)
  private readonly geofences = toSignal(
    this.geofenceApi.getGeofences().pipe(
      catchError((error: HttpErrorResponse) => {
        this.geofenceError.set(error);
        return of([]);
      }),
    ),
    { initialValue: [] },
  );

  private readonly map = signal<L.Map | null>(null);
  private readonly markers = new Map<number, L.Marker>(); // aracın numarası -> haritadaki işareti
  private alarmMarker: L.CircleMarker | null = null;
  private trailLayer: L.LayerGroup | null = null; // izin parçaları (soluktan koyuya)
  private geofenceLayer: L.LayerGroup | null = null; // bölge daireleri ve adları
  // En son neye yakınlaştırıldı ("hepsi", "arac-3", "alarm-46"). Aynı hedefe bir kez yakınlaştırılır:
  // her canlı güncellemede tekrar yakınlaştırsak kullanıcının kaydırdığı harita geri zıplardı.
  private zoomedTo: string | null = null;

  constructor() {
    // Leaflet gerçek bir HTML elemanına ihtiyaç duyar, bu yüzden haritayı sayfa çizildikten sonra oluşturuyoruz
    afterNextRender(() => {
      const map = L.map(this.mapElement().nativeElement).setView(ISTANBUL, 11);
      L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> katkıcıları',
      }).addTo(map);
      // Bölgeler için ayrı katman: çizgilerin (iz, 400) ve araç işaretlerinin (600) altında kalsın, onları örtmesin
      map.createPane(GEOFENCE_PANE).style.zIndex = '350';
      this.map.set(map);
    });

    // Harita hazırsa ve yeni konumlar geldiyse işaretleri güncelle (signal'ler değiştikçe kendiliğinden çalışır)
    effect(() => {
      const map = this.map();
      const vehicles = this.live.vehicles();
      if (map && vehicles) {
        this.updateMarkers(map, vehicles);
        this.zoomToVehicles(map);
      }
    });

    // ?alarm= ile gelindiyse alarm API'den gelince haritada göster
    effect(() => {
      const map = this.map();
      if (map) {
        this.showAlarm(map, this.alarm());
      }
    });

    // Bölgeler API'den gelince çiz
    effect(() => {
      const map = this.map();
      if (map) {
        this.drawGeofences(map, this.geofences());
      }
    });

    // Seçili aracın izi değiştikçe çizgiyi güncelle
    effect(() => {
      const map = this.map();
      if (map) {
        this.drawTrail(map, this.trail());
      }
    });

    // Sayfadan çıkınca haritayı temizle
    inject(DestroyRef).onDestroy(() => this.map()?.remove());
  }

  private updateMarkers(map: L.Map, vehicles: VehicleLastLocation[]): void {
    const onMap = new Set<number>();

    for (const v of vehicles) {
      if (v.latitude === null || v.longitude === null) {
        continue; // henüz konumu yok
      }
      onMap.add(v.vehicleId);
      const position: L.LatLngTuple = [v.latitude, v.longitude];

      let marker = this.markers.get(v.vehicleId);
      if (marker) {
        marker.setLatLng(position); // var olan işareti yeni konuma taşı
      } else {
        marker = L.marker(position, { icon: markerIcon(v.plate) }).bindPopup('').addTo(map);
        // Balon açılınca o aracın izi çizilir, kapanınca silinir
        const vehicleId = v.vehicleId;
        marker.on('popupopen', () => this.selectedVehicleId.set(vehicleId));
        marker.on('popupclose', () => this.selectedVehicleId.set(null));
        this.markers.set(v.vehicleId, marker);
      }
      marker.getElement()?.classList.toggle('speeding', isSpeeding(v));
      marker.setPopupContent(this.popupHtml(v));
    }

    // Listeden çıkan araçların işaretini kaldır
    for (const [vehicleId, marker] of this.markers) {
      if (!onMap.has(vehicleId)) {
        marker.remove();
        this.markers.delete(vehicleId);
      }
    }
  }

  // ?arac=3 ise o araca yakınlaşıp bilgi balonunu açar, yoksa bütün araçları sığdırır.
  // ?alarm= varsa hiçbir şey yapmaz: harita alarmın yerine yakınlaşır (showAlarm). Alarm alınamadıysa hepsini gösterir.
  private zoomToVehicles(map: L.Map): void {
    if (this.alarmId() !== undefined && !this.alarmError()) {
      return;
    }
    const vehicleId = this.vehicleId();
    const marker = vehicleId === undefined ? undefined : this.markers.get(vehicleId);
    const target = marker ? `arac-${vehicleId}` : 'hepsi';
    if (this.zoomedTo === target || this.markers.size === 0) {
      return;
    }

    if (marker) {
      map.setView(marker.getLatLng(), FOCUS_ZOOM);
      marker.openPopup();
    } else {
      map.closePopup(); // tek bir araca bakıyorduk, genel görünüme dönerken onun balonu açık kalmasın
      const bounds = L.featureGroup([...this.markers.values()]).getBounds();
      map.fitBounds(bounds, { padding: [40, 40], maxZoom: 14 });
    }
    this.zoomedTo = target;
  }

  // Alarmın olduğu yere kırmızı bir halka koyar ve oraya yakınlaşır (aracın şu anki yeri değil, alarm anındaki yeri)
  private showAlarm(map: L.Map, alarm: Alarm | null): void {
    this.alarmMarker?.remove();
    this.alarmMarker = null;
    if (!alarm || alarm.latitude === null || alarm.longitude === null) {
      return;
    }

    const position: L.LatLngTuple = [alarm.latitude, alarm.longitude];
    this.alarmMarker = L.circleMarker(position, { radius: 12, color: '#c62828', weight: 3, fillOpacity: 0.15 })
      .bindPopup(this.alarmPopupHtml(alarm))
      .addTo(map);
    map.setView(position, FOCUS_ZOOM);
    this.alarmMarker.openPopup();
    this.zoomedTo = `alarm-${alarm.alarmId}`;
  }

  // Aracın geçtiği yol: konumları sırayla birleştiren mavi çizgi (araç işaretlerinin altında kalır).
  // Eski parçalar açık mavi, yeni parçalar koyu mavi. Parçalar eskiden yeniye eklenir, böylece araç
  // aynı yoldan geri döndüyse yeni (koyu) çizgi eskisinin üstünde kalır.
  private drawTrail(map: L.Map, points: LocationUpdate[]): void {
    this.trailLayer ??= L.layerGroup().addTo(map);
    this.trailLayer.clearLayers();
    for (const segment of trailSegments(points, TRAIL_MINUTES)) {
      const latLngs = segment.points.map((p): L.LatLngTuple => [p.latitude, p.longitude]);
      L.polyline(latLngs, { color: trailColor(segment.fade), weight: 4, opacity: 1 }).addTo(this.trailLayer);
    }
  }

  // Bölgeler: kesik çizgili daire, ortasında adı ve hangi geçişte alarm verdiği.
  // interactive: false -> dairenin üstüne tıklamak, altındaki haritayı sürüklemeyi ya da araca tıklamayı engellemesin
  private drawGeofences(map: L.Map, zones: Geofence[]): void {
    this.geofenceLayer ??= L.layerGroup().addTo(map);
    this.geofenceLayer.clearLayers();
    for (const zone of zones) {
      L.circle([zone.latitude, zone.longitude], {
        radius: zone.radiusMeters, // metre: harita yakınlaştıkça daire de büyür
        pane: GEOFENCE_PANE,
        className: 'geofence', // renk styles.scss'te
        weight: 2,
        dashArray: '6 4',
        fillOpacity: 0.12,
        interactive: false,
      })
        .bindTooltip(`${escapeHtml(zone.name)}<br>${geofenceAlarmText(zone)}`, {
          permanent: true,
          direction: 'center',
          className: 'geofence-label',
          pane: GEOFENCE_PANE,
        })
        .addTo(this.geofenceLayer);
    }
  }

  private alarmPopupHtml(alarm: Alarm): string {
    return `
      <strong>${escapeHtml(alarm.plate)}</strong><br>
      ${escapeHtml(alarm.message)}<br>
      ${formatDate(alarm.createdAt, 'dd.MM.yyyy HH:mm:ss', this.locale)}`;
  }

  private popupHtml(v: VehicleLastLocation): string {
    const speed = formatNumber(v.speedKmh ?? 0, this.locale, '1.0-1');
    const fuel = v.fuelPercent === null ? '—' : '%' + formatNumber(v.fuelPercent, this.locale, '1.0-1');
    const lastSignal = v.recordedAt ? formatDate(v.recordedAt, 'HH:mm:ss', this.locale) : '—';
    const ignition = v.ignitionOn === null ? '—' : v.ignitionOn ? 'Açık' : 'Kapalı';

    return `
      <strong>${escapeHtml(v.plate)}</strong><br>
      ${escapeHtml(v.brand)} ${escapeHtml(v.model)}<br>
      Sürücü: ${escapeHtml(v.driverName ?? '—')}<br>
      Hız: <span class="${isSpeeding(v) ? 'danger' : ''}">${speed} km/s</span> / ${v.speedLimitKmh}<br>
      Yakıt: <span class="${isLowFuel(v) ? 'warning' : ''}">${fuel}</span><br>
      Kontak: ${ignition}<br>
      Son sinyal: ${lastSignal}`;
  }
}

// İz rengi: fade 0 = koyu mavi (#1a62c4), fade 1 = açık mavi (#b4cbee); arası iki rengin karışımı.
// Saydamlık yerine açık renk kullanıyoruz: saydam çizgiler üst üste binince birbirinin içinden görünürdü.
function trailColor(fade: number): string {
  const dark = [26, 98, 196];
  const light = [180, 203, 238];
  const [r, g, b] = dark.map((d, i) => Math.round(d + (light[i] - d) * fade));
  return `rgb(${r}, ${g}, ${b})`;
}

// Haritadaki işaret: renkli nokta + yanında plaka
function markerIcon(plate: string): L.DivIcon {
  return L.divIcon({
    className: 'vehicle-marker',
    html: `<span class="label">${escapeHtml(plate)}</span>`,
    iconSize: [14, 14],
    popupAnchor: [0, -8],
  });
}

// Veritabanından gelen metni HTML'e koymadan önce zararsız hale getir
// (ör. sürücü adına "<script>" yazılmışsa çalışmasın, düz yazı olarak görünsün)
function escapeHtml(text: string): string {
  const replacements: Record<string, string> = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
  return text.replace(/[&<>"']/g, (ch) => replacements[ch]);
}
