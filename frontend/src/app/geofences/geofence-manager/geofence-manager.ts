import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { afterNextRender, Component, computed, DestroyRef, effect, ElementRef, inject, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import * as L from 'leaflet';
import { catchError, EMPTY, finalize, map, merge, startWith, Subject, switchMap, tap } from 'rxjs';
import { LiveHub } from '../../core/live-hub';
import { createGeofencePane, createMap, geofenceCircle, removeMap } from '../../map/map-helpers';
import { ApiErrorAlert } from '../../shared/api-error-alert/api-error-alert';
import {
  DEFAULT_RADIUS_METERS,
  Geofence,
  geofenceAlarmText,
  GeofenceInput,
  MAX_RADIUS_METERS,
  MIN_RADIUS_METERS,
  roundCoordinate,
  roundRadius,
} from '../geofence';
import { GeofenceApi } from '../geofence-api';

const EDIT_ZOOM = 14; // yeni bölge eklerken en az bu kadar yakın (mahalle düzeyi), daire görünsün
const SLIDER_MAX_METERS = 5000; // kaydırma çubuğu 50 m - 5 km; daha büyüğü kutuya yazılarak
// Leaflet'in dünya yarıçapıyla (6.371 km) 1 derecenin metre karşılığı; yarıçap noktası dairenin tam kenarına otursun
const METERS_PER_DEGREE = (6_371_000 * Math.PI) / 180;

// Hiçbir geçişte alarm vermeyen bölge işe yaramaz: en az biri seçilmeli
function atLeastOneAlarm(group: AbstractControl): ValidationErrors | null {
  const { alarmOnEnter, alarmOnExit } = group.value as GeofenceInput;
  return alarmOnEnter || alarmOnExit ? null : { noAlarm: true };
}

// Merkezin `meters` doğusundaki nokta: yarıçapı değiştirmek için sürüklenen tutamak burada durur
function eastOf(center: L.LatLng, meters: number): L.LatLng {
  return L.latLng(center.lat, center.lng + meters / (METERS_PER_DEGREE * Math.cos((center.lat * Math.PI) / 180)));
}

// Haritada sürüklenen yuvarlak tutamak (merkez ya da kenar)
function handleIcon(kind: 'center' | 'edge'): L.DivIcon {
  return L.divIcon({ className: `geofence-handle ${kind}`, iconSize: kind === 'center' ? [18, 18] : [14, 14] });
}

// Düzenlenen bölgenin haritadaki parçaları: daire, merkez tutamağı (taşımak için), kenar tutamağı (yarıçap için)
interface EditLayer {
  group: L.LayerGroup;
  circle: L.Circle;
  center: L.Marker;
  edge: L.Marker;
}

// Bölgeler sayfası: solda bölge listesi ya da düzenleme formu, sağda harita.
// Bölge haritada yerleştirilir: haritaya tıklanan ya da merkez noktasının sürüklendiği yer bölgenin merkezi olur,
// kenardaki nokta sürüklenerek büyütülüp küçültülür.
@Component({
  imports: [ApiErrorAlert, DecimalPipe, ReactiveFormsModule],
  selector: 'app-geofence-manager',
  styleUrl: './geofence-manager.scss',
  templateUrl: './geofence-manager.html',
})
export class GeofenceManager {
  private readonly geofenceApi = inject(GeofenceApi);
  private readonly hub = inject(LiveHub);
  private readonly mapElement = viewChild.required<ElementRef<HTMLDivElement>>('map');

  protected readonly alarmText = geofenceAlarmText;
  protected readonly minRadius = MIN_RADIUS_METERS;
  protected readonly maxRadius = MAX_RADIUS_METERS;
  protected readonly sliderMax = SLIDER_MAX_METERS;

  protected readonly listError = signal<HttpErrorResponse | null>(null);
  protected readonly actionError = signal<HttpErrorResponse | null>(null);
  protected readonly saveError = signal<HttpErrorResponse | null>(null);
  protected readonly saving = signal(false);

  // Bölgeler: sayfa açılınca, bu sayfada kaydedilip silinince, başka bir tarayıcıda değişince ve canlı bağlantı
  // koptuktan sonra yeniden kurulunca çekilir. Çekilemezse eldeki liste kalır, üstünde hata yazar.
  private readonly reload = new Subject<void>();
  protected readonly zones = toSignal(
    merge(this.reload, this.hub.geofencesChanged$, this.hub.reconnected$).pipe(
      startWith(undefined),
      switchMap(() =>
        this.geofenceApi.getGeofences().pipe(
          tap(() => this.listError.set(null)),
          catchError((error: HttpErrorResponse) => {
            this.listError.set(error);
            return EMPTY;
          }),
        ),
      ),
    ),
    { initialValue: null },
  );

  // Listede ya da haritada seçilen bölge (vurgulanır)
  protected readonly selectedId = signal<number | null>(null);
  // Düzenlenen bölge: null = düzenleme yok (liste görünür), 'new' = yeni bölge, sayı = o numaralı bölge
  protected readonly editing = signal<number | 'new' | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      name: ['', [Validators.required, Validators.maxLength(100), Validators.pattern(/\S/)]],
      latitude: [0],
      longitude: [0],
      radiusMeters: [
        DEFAULT_RADIUS_METERS,
        [Validators.required, Validators.min(MIN_RADIUS_METERS), Validators.max(MAX_RADIUS_METERS)],
      ],
      alarmOnEnter: [true],
      alarmOnExit: [true],
    },
    { validators: atLeastOneAlarm },
  );

  // Formun o anki değeri. Harita sürüklenince (Leaflet olayı) form kodla değişiyor; signal olunca daire ve
  // ekrandaki yarıçap da güncellenir.
  private readonly formChanges = toSignal(this.form.valueChanges);
  protected readonly draft = computed(() => {
    this.formChanges();
    return this.form.getRawValue();
  });

  private readonly map = signal<L.Map | null>(null);
  private zoneLayer: L.LayerGroup | null = null;
  private edit: EditLayer | null = null;
  private draggingEdge = false; // kenar tutamağı sürüklenirken onu yerine geri koyma (elden kaçardı)
  private fitted = false; // bölgelere bir kez yakınlaşılır, sonra kullanıcının kaydırdığı yerde kalır

  constructor() {
    afterNextRender(() => {
      const map = createMap(this.mapElement().nativeElement);
      createGeofencePane(map);
      // Düzenlerken haritaya tıklanan yer bölgenin yeni merkezi olur
      map.on('click', (event) => this.moveCenter(event.latlng));
      this.map.set(map);
    });

    // Bölgeleri çiz. Düzenlenen bölge listeden değil, düzenleme dairesiyle çizilir.
    effect(() => {
      const map = this.map();
      const zones = this.zones();
      if (map && zones) {
        this.drawZones(map, zones, this.editing(), this.selectedId());
        this.fitZones(map, zones);
      }
    });

    // Düzenleme dairesi: düzenleme açıkken formdaki merkez ve yarıçapla
    effect(() => {
      const map = this.map();
      if (map) {
        this.drawEditLayer(map, this.editing() === null ? null : this.draft());
      }
    });

    inject(DestroyRef).onDestroy(() => {
      const map = this.map();
      if (map) {
        removeMap(map);
      }
    });
  }

  // Alan dokunulmuş ve geçersizse hata yazısı gösterilir (boş formu açar açmaz kırmızı görmesin)
  protected invalid(name: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[name];
    return control.invalid && control.touched;
  }

  // Listeden seçilince haritada o bölgeye gidilir; haritada tıklanınca listede işaretlenir
  protected select(zone: Geofence, focusMap: boolean): void {
    this.selectedId.set(zone.geofenceId);
    const map = this.map();
    if (focusMap && map) {
      this.focus(map, zone);
    }
    if (!focusMap) {
      document.getElementById(`zone-${zone.geofenceId}`)?.scrollIntoView({ block: 'nearest' });
    }
  }

  // Yeni bölge haritanın ortasında başlar; tıklayarak ya da sürükleyerek yeri değiştirilir
  protected startNew(): void {
    const map = this.map();
    if (!map) {
      return;
    }
    const center = map.getCenter();
    if (map.getZoom() < EDIT_ZOOM) {
      map.setView(center, EDIT_ZOOM);
    }
    this.startEditing('new', {
      name: '',
      latitude: roundCoordinate(center.lat),
      longitude: roundCoordinate(center.lng),
      radiusMeters: DEFAULT_RADIUS_METERS,
      alarmOnEnter: true,
      alarmOnExit: true,
    });
  }

  protected startEdit(zone: Geofence): void {
    const { geofenceId, ...values } = zone;
    this.selectedId.set(geofenceId);
    this.startEditing(geofenceId, values);
    const map = this.map();
    if (map) {
      this.focus(map, zone);
    }
  }

  protected cancel(): void {
    this.editing.set(null);
    this.saveError.set(null);
  }

  // Kaydırma çubuğu: kutuyla aynı forma yazar (iki kutu aynı form alanına bağlanınca birbirini güncellemiyor)
  protected setRadius(meters: number): void {
    this.form.controls.radiusMeters.setValue(meters);
    this.form.controls.radiusMeters.markAsTouched();
  }

  protected save(): void {
    const editing = this.editing();
    if (editing === null) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const zone = this.form.getRawValue();
    // Kaydedilen bölgenin numarası (yeni bölgede API verir); kaydedince listede o seçili kalır
    const request =
      editing === 'new'
        ? this.geofenceApi.createGeofence(zone).pipe(map((created) => created.geofenceId))
        : this.geofenceApi.updateGeofence(editing, zone).pipe(map(() => editing));
    this.saving.set(true);
    this.saveError.set(null);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: (geofenceId) => {
        this.editing.set(null);
        this.selectedId.set(geofenceId);
        this.reload.next();
      },
      error: (error: HttpErrorResponse) => this.saveError.set(error),
    });
  }

  // Silmeden önce sorar. Bölge pasife alınır: artık alarm üretmez, eski alarmları silinmez.
  protected remove(zone: Geofence): void {
    const question =
      `"${zone.name}" bölgesi silinsin mi?\n\n` +
      'Bu bölge için artık alarm üretilmez. Daha önce üretilmiş alarmlar silinmez.';
    if (!confirm(question)) {
      return;
    }
    this.actionError.set(null);
    this.geofenceApi.deleteGeofence(zone.geofenceId).subscribe({
      next: () => this.reload.next(),
      error: (error: HttpErrorResponse) => this.actionError.set(error),
    });
  }

  private startEditing(editing: number | 'new', values: GeofenceInput): void {
    this.form.reset(values);
    this.saveError.set(null);
    this.actionError.set(null);
    this.editing.set(editing);
  }

  private moveCenter(position: L.LatLng): void {
    if (this.editing() === null) {
      return;
    }
    const { lat, lng } = position.wrap(); // boylam -180..180 arasında kalsın
    this.form.patchValue({ latitude: roundCoordinate(lat), longitude: roundCoordinate(lng) });
  }

  private focus(map: L.Map, zone: GeofenceInput): void {
    map.fitBounds(L.latLng(zone.latitude, zone.longitude).toBounds(zone.radiusMeters * 2), {
      padding: [40, 40],
      maxZoom: 16,
    });
  }

  // Sayfa açılınca bütün bölgeler görünecek kadar yakınlaş (bir kez)
  private fitZones(map: L.Map, zones: Geofence[]): void {
    if (this.fitted || zones.length === 0) {
      return;
    }
    this.fitted = true;
    const bounds = L.latLngBounds([]);
    for (const zone of zones) {
      bounds.extend(L.latLng(zone.latitude, zone.longitude).toBounds(zone.radiusMeters * 2));
    }
    map.fitBounds(bounds, { padding: [40, 40] });
  }

  private drawZones(map: L.Map, zones: Geofence[], editing: number | 'new' | null, selectedId: number | null): void {
    this.zoneLayer ??= L.layerGroup().addTo(map);
    this.zoneLayer.clearLayers();
    for (const zone of zones) {
      if (zone.geofenceId === editing) {
        continue;
      }
      // Düzenleme sırasında daireler tıklanamaz: tıklama altındaki haritaya gitsin (merkezi taşır)
      const circle = geofenceCircle(zone, editing === null).addTo(this.zoneLayer);
      if (zone.geofenceId === selectedId) {
        circle.getElement()?.classList.add('selected');
      }
      circle.on('click', () => this.select(zone, false));
    }
  }

  private drawEditLayer(map: L.Map, draft: GeofenceInput | null): void {
    if (!draft) {
      this.edit?.group.remove();
      this.edit = null;
      return;
    }
    const center = L.latLng(draft.latitude, draft.longitude);
    // Kutuya yazılırken (ör. "5" -> "50" -> "500") yarıçap bir an geçersiz olabilir, daire sınırlar içinde çizilir
    const radius = Math.min(MAX_RADIUS_METERS, Math.max(MIN_RADIUS_METERS, draft.radiusMeters || 0));
    this.edit ??= this.createEditLayer(map, center, radius);
    this.edit.circle.setLatLng(center).setRadius(radius);
    this.edit.center.setLatLng(center);
    if (!this.draggingEdge) {
      this.edit.edge.setLatLng(eastOf(center, radius));
    }
  }

  private createEditLayer(map: L.Map, center: L.LatLng, radius: number): EditLayer {
    const circle = L.circle(center, { radius, className: 'geofence-edit', weight: 2, fillOpacity: 0.15, interactive: false });
    const centerHandle = L.marker(center, { icon: handleIcon('center'), draggable: true, title: 'Sürükle: bölgeyi taşı' });
    const edge = L.marker(eastOf(center, radius), {
      icon: handleIcon('edge'),
      draggable: true,
      title: 'Sürükle: bölgeyi büyüt ya da küçült',
    });

    centerHandle.on('drag', () => this.moveCenter(centerHandle.getLatLng()));
    edge.on('dragstart', () => (this.draggingEdge = true));
    edge.on('drag', () => this.setRadius(roundRadius(map.distance(circle.getLatLng(), edge.getLatLng()))));
    edge.on('dragend', () => {
      this.draggingEdge = false;
      edge.setLatLng(eastOf(circle.getLatLng(), circle.getRadius())); // tutamak dairenin kenarına geri otursun
    });

    return { group: L.layerGroup([circle, centerHandle, edge]).addTo(map), circle, center: centerHandle, edge };
  }
}
