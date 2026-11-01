import { DatePipe, DecimalPipe, formatDate } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  linkedSignal,
  LOCALE_ID,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import * as L from 'leaflet';
import { finalize, forkJoin, Subscription } from 'rxjs';
import { Alarm, ALARM_TYPE_LABELS } from '../../alarms/alarm';
import { AlarmApi } from '../../alarms/alarm-api';
import { ApiErrorAlert } from '../../shared/api-error-alert/api-error-alert';
import { LiveVehicles } from '../../vehicles/live-vehicles';
import { VehicleApi } from '../../vehicles/vehicle-api';
import { createMap, escapeHtml, markerIcon, toId } from '../map-helpers';
import {
  findStops,
  formatDuration,
  PLAYBACK_SPEEDS,
  PlaybackSpeed,
  SECONDS_PER_SECOND_AT_1X,
  Stop,
  Track,
} from './playback';

// Geçmiş rota oynatma: seçilen aracın seçilen saat aralığındaki yolu haritada baştan oynatılır.
// Duraklar ve o aralıktaki alarmlar haritada işaretlenir.
@Component({
  imports: [ApiErrorAlert, DatePipe, DecimalPipe],
  selector: 'app-route-playback',
  styleUrl: './route-playback.scss',
  templateUrl: './route-playback.html',
})
export class RoutePlayback {
  private readonly live = inject(LiveVehicles);
  private readonly vehicleApi = inject(VehicleApi);
  private readonly alarmApi = inject(AlarmApi);
  private readonly locale = inject(LOCALE_ID);
  private readonly destroyRef = inject(DestroyRef);
  private readonly mapElement = viewChild.required<ElementRef<HTMLDivElement>>('map');

  // Araç listesindeki "Geçmiş" bağlantısından gelen ?arac=3 (araç seçili gelir)
  readonly preselectedVehicleId = input(undefined, { alias: 'arac', transform: toId });

  protected readonly vehicles = this.live.vehicles;
  protected readonly vehiclesError = this.live.apiError;
  protected readonly speeds = PLAYBACK_SPEEDS;
  protected readonly secondsPerSecond = SECONDS_PER_SECOND_AT_1X;

  // Sorgu formu. Saatler tarayıcının saat diliminde girilir, API'ye UTC gönderilir.
  protected readonly vehicleId = linkedSignal(() => this.preselectedVehicleId() ?? null);
  protected readonly date = signal(formatDate(new Date(), 'yyyy-MM-dd', this.locale));
  protected readonly fromTime = signal('00:00');
  protected readonly toTime = signal('23:59');
  protected readonly formError = signal<string | null>(null);

  protected readonly loading = signal(false);
  protected readonly apiError = signal<HttpErrorResponse | null>(null);
  protected readonly loaded = signal(false); // en az bir sorgu cevaplandı mı ("konum yok" mesajı için)
  protected readonly track = signal<Track | null>(null); // konum yoksa null
  protected readonly alarms = signal<Alarm[]>([]);
  protected readonly stops = computed(() => findStops(this.track()?.points ?? []));
  private plate = '';
  private loadSubscription: Subscription | null = null;

  // Oynatma: saat (ms, UTC), oynuyor mu, hız
  protected readonly time = signal(0);
  protected readonly playing = signal(false);
  protected readonly speed = signal<PlaybackSpeed>(1);
  // Ekranda gösterilen bilgiler (hız, yakıt, kontak) için o anki ya da ondan önceki son konum
  protected readonly current = computed(() => {
    const track = this.track();
    return track ? track.points[track.indexAt(this.time())] : null;
  });

  private animationFrame: number | null = null;
  private lastFrameAt = 0;

  private readonly map = signal<L.Map | null>(null);
  private routeLayer: L.LayerGroup | null = null; // güzergâhın tamamı, duraklar, alarmlar
  private playedLine: L.Polyline | null = null; // gidilmiş yol
  private vehicleMarker: L.Marker | null = null;
  private drawnTrack: Track | null = null; // oynatma çizimleri hangi güzergâh için
  private drawnIndex = -1; // gidilmiş yol en son hangi konuma kadar çizildi

  constructor() {
    // Leaflet gerçek bir HTML elemanına ihtiyaç duyar, bu yüzden haritayı sayfa çizildikten sonra oluşturuyoruz
    afterNextRender(() => this.map.set(createMap(this.mapElement().nativeElement)));

    // Yeni geçmiş yüklenince güzergâh, duraklar ve alarmlar çizilir, harita güzergâha sığdırılır
    effect(() => {
      const map = this.map();
      if (map) {
        this.drawRoute(map, this.track(), this.stops(), this.alarms());
      }
    });

    // Oynatma saati ilerledikçe araç işareti ve gidilmiş yol güncellenir
    effect(() => {
      const map = this.map();
      if (map) {
        this.drawProgress(map, this.track(), this.time());
      }
    });

    this.destroyRef.onDestroy(() => {
      this.pause();
      this.map()?.remove();
    });
  }

  // Araç seçilince tarih, aracın son sinyalinin günü olur (çoğu zaman bakılmak istenen gün)
  protected selectVehicle(value: string): void {
    const vehicleId = Number(value);
    this.vehicleId.set(vehicleId);
    const recordedAt = this.vehicles()?.find((v) => v.vehicleId === vehicleId)?.recordedAt;
    if (recordedAt) {
      this.date.set(formatDate(recordedAt, 'yyyy-MM-dd', this.locale));
    }
  }

  protected load(): void {
    const vehicleId = this.vehicleId();
    // "2026-10-31T08:00" gibi saat dilimi yazılmamış tarih, tarayıcının saat diliminde okunur
    const from = new Date(`${this.date()}T${this.fromTime()}`);
    const to = new Date(`${this.date()}T${this.toTime()}:59`);
    if (vehicleId === null) {
      this.formError.set('Bir araç seç.');
      return;
    }
    if (isNaN(from.getTime()) || isNaN(to.getTime())) {
      this.formError.set('Tarih ve saatleri doldur.');
      return;
    }
    if (to <= from) {
      this.formError.set('Bitiş saati başlangıçtan sonra olmalı.');
      return;
    }

    this.pause();
    this.formError.set(null);
    this.apiError.set(null);
    this.loading.set(true);
    this.plate = this.vehicles()?.find((v) => v.vehicleId === vehicleId)?.plate ?? '';
    this.loadSubscription?.unsubscribe(); // önceki sorgu hâlâ sürüyorsa cevabı beklenmez
    this.loadSubscription = forkJoin({
      points: this.vehicleApi.getHistory(vehicleId, from, to),
      alarms: this.alarmApi.getVehicleAlarms(vehicleId, from, to),
    })
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: ({ points, alarms }) => {
          const track = points.length > 0 ? new Track(points) : null;
          this.track.set(track);
          this.alarms.set(alarms);
          this.time.set(track?.start ?? 0);
          this.loaded.set(true);
        },
        error: (error: HttpErrorResponse) => this.apiError.set(error),
      });
  }

  protected play(): void {
    const track = this.track();
    if (!track) {
      return;
    }
    if (this.time() >= track.end) {
      this.time.set(track.start); // sondaysa baştan oynat
    }
    this.playing.set(true);
    this.lastFrameAt = performance.now();
    this.animationFrame = requestAnimationFrame(this.tick);
  }

  protected pause(): void {
    this.playing.set(false);
    if (this.animationFrame !== null) {
      cancelAnimationFrame(this.animationFrame);
      this.animationFrame = null;
    }
  }

  // İlerleme çubuğu sürüklenince ya da durağa/alarma tıklanınca o ana gidilir
  protected seek(time: number): void {
    this.time.set(time);
  }

  // Tarayıcı her ekran yenilemesinde (saniyede ~60 kez) çağırır. Saat, geçen gerçek süre x hız kadar ilerler.
  private readonly tick = (now: number): void => {
    const track = this.track();
    if (!track) {
      return;
    }
    const elapsedMs = now - this.lastFrameAt;
    this.lastFrameAt = now;
    const time = track.advance(this.time(), elapsedMs * this.speed() * SECONDS_PER_SECOND_AT_1X);
    this.time.set(time);
    if (time >= track.end) {
      this.pause();
    } else {
      this.animationFrame = requestAnimationFrame(this.tick);
    }
  };

  private drawRoute(map: L.Map, track: Track | null, stops: Stop[], alarms: Alarm[]): void {
    this.routeLayer ??= L.layerGroup().addTo(map);
    this.routeLayer.clearLayers();
    if (!track) {
      return;
    }

    // Güzergâhın tamamı soluk gri; üstüne oynatıldıkça gidilmiş yol mavi çizilir (drawProgress)
    const route = L.polyline(track.latLngsUntil(track.points.length - 1), { color: '#9aa5b4', weight: 4 });
    route.addTo(this.routeLayer);

    for (const stop of stops) {
      L.marker([stop.latitude, stop.longitude], {
        icon: L.divIcon({ className: 'stop-marker', html: 'P', iconSize: [18, 18] }),
      })
        .bindPopup(this.stopPopupHtml(stop))
        .on('click', () => this.seek(Date.parse(stop.start)))
        .addTo(this.routeLayer);
    }

    for (const alarm of alarms) {
      if (alarm.latitude === null || alarm.longitude === null) {
        continue;
      }
      L.circleMarker([alarm.latitude, alarm.longitude], { radius: 8, color: '#c62828', weight: 3, fillOpacity: 0.3 })
        .bindPopup(this.alarmPopupHtml(alarm))
        .on('click', () => this.seek(Math.max(track.start, Date.parse(alarm.createdAt))))
        .addTo(this.routeLayer);
    }

    map.fitBounds(route.getBounds(), { padding: [40, 40], maxZoom: 16 });
  }

  private drawProgress(map: L.Map, track: Track | null, time: number): void {
    if (track !== this.drawnTrack) {
      // Yeni güzergâh: önceki aracın işareti ve gidilmiş yolu kaldırılır
      this.vehicleMarker?.remove();
      this.playedLine?.remove();
      this.vehicleMarker = this.playedLine = null;
      this.drawnTrack = track;
      this.drawnIndex = -1;
    }
    if (!track) {
      return;
    }

    const position = track.positionAt(time);
    this.vehicleMarker ??= L.marker(position, { icon: markerIcon(this.plate), zIndexOffset: 1000 }).addTo(map);
    this.vehicleMarker.setLatLng(position);

    // Gidilmiş yol sadece yeni bir konuma geçilince yeniden çizilir (binlerce noktalı çizgiyi her karede
    // baştan çizmek yavaş olurdu)
    const index = track.indexAt(time);
    if (index !== this.drawnIndex) {
      this.playedLine ??= L.polyline([], { color: '#1a62c4', weight: 5 }).addTo(map);
      this.playedLine.setLatLngs(track.latLngsUntil(index));
      this.drawnIndex = index;
    }
  }

  private stopPopupHtml(stop: Stop): string {
    const start = formatDate(stop.start, 'HH:mm', this.locale);
    const end = formatDate(stop.end, 'HH:mm', this.locale);
    return `<strong>Durak</strong><br>${start} – ${end} (${formatDuration(stop.seconds)})`;
  }

  private alarmPopupHtml(alarm: Alarm): string {
    return `
      <strong>${ALARM_TYPE_LABELS[alarm.alarmType]}</strong><br>
      ${escapeHtml(alarm.message)}<br>
      ${formatDate(alarm.createdAt, 'HH:mm:ss', this.locale)}`;
  }
}
