import { formatDate, formatNumber } from '@angular/common';
import {
  afterNextRender,
  Component,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  LOCALE_ID,
  signal,
  viewChild,
} from '@angular/core';
import * as L from 'leaflet';
import { isLowFuel, isSpeeding, VehicleLastLocation } from '../../models/vehicle-last-location';
import { LiveVehicles } from '../../services/live-vehicles';

const ISTANBUL: L.LatLngTuple = [41.03, 28.98];

@Component({
  imports: [],
  selector: 'app-vehicle-map',
  styleUrl: './vehicle-map.scss',
  templateUrl: './vehicle-map.html',
})
export class VehicleMap {
  private readonly live = inject(LiveVehicles);
  private readonly locale = inject(LOCALE_ID);
  private readonly mapElement = viewChild.required<ElementRef<HTMLDivElement>>('map');

  protected readonly apiError = this.live.apiError;

  private readonly map = signal<L.Map | null>(null);
  private readonly markers = new Map<number, L.Marker>(); // aracın numarası -> haritadaki işareti
  private zoomedToVehicles = false;

  constructor() {
    // Leaflet gerçek bir HTML elemanına ihtiyaç duyar, bu yüzden haritayı sayfa çizildikten sonra oluşturuyoruz
    afterNextRender(() => {
      const map = L.map(this.mapElement().nativeElement).setView(ISTANBUL, 11);
      L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> katkıcıları',
      }).addTo(map);
      this.map.set(map);
    });

    // Harita hazırsa ve yeni konumlar geldiyse işaretleri güncelle (signal'ler değiştikçe kendiliğinden çalışır)
    effect(() => {
      const map = this.map();
      const vehicles = this.live.vehicles();
      if (map && vehicles) {
        this.updateMarkers(map, vehicles);
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

    // İlk seferde haritayı bütün araçlar görünecek şekilde yakınlaştır
    if (!this.zoomedToVehicles && this.markers.size > 0) {
      const bounds = L.featureGroup([...this.markers.values()]).getBounds();
      map.fitBounds(bounds, { padding: [40, 40], maxZoom: 14 });
      this.zoomedToVehicles = true;
    }
  }

  private popupHtml(v: VehicleLastLocation): string {
    const speed = formatNumber(v.speedKmh ?? 0, this.locale, '1.0-1');
    const fuel = v.fuelPercent === null ? '—' : '%' + formatNumber(v.fuelPercent, this.locale, '1.0-1');
    const lastSignal = v.recordedAt ? formatDate(v.recordedAt, 'HH:mm:ss', this.locale) : '—';

    return `
      <strong>${escapeHtml(v.plate)}</strong><br>
      ${escapeHtml(v.brand)} ${escapeHtml(v.model)}<br>
      Sürücü: ${escapeHtml(v.driverName ?? '—')}<br>
      Hız: <span class="${isSpeeding(v) ? 'danger' : ''}">${speed} km/s</span> / ${v.speedLimitKmh}<br>
      Yakıt: <span class="${isLowFuel(v) ? 'warning' : ''}">${fuel}</span><br>
      Son sinyal: ${lastSignal}`;
  }
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
