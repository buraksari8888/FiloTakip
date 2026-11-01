import { HttpErrorResponse } from '@angular/common/http';
import { afterNextRender, Component, computed, ElementRef, inject, output, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { catchError, finalize, of } from 'rxjs';
import { driverLabel } from '../../drivers/driver';
import { DriverApi } from '../../drivers/driver-api';
import { ApiErrorAlert } from '../../shared/api-error-alert/api-error-alert';
import { LiveVehicles } from '../live-vehicles';
import { searchKey } from '../vehicle-search';
import { VehicleApi } from '../vehicle-api';
import { DEFAULT_SPEED_LIMITS, VEHICLE_TYPE_LABELS, VEHICLE_TYPES, VehicleType } from '../vehicle-type';

// Türkiye plakası: il kodu (01-81) + 1-3 harf + 2-4 rakam; boşluk ve küçük harf serbest ("34abc123").
// API'deki VehicleRules.NormalizePlate ile aynı kural (biçimi API düzeltir: "34 ABC 123").
const PLATE_PATTERN = /^\s*(0[1-9]|[1-7][0-9]|8[01])\s*[a-zA-Z]{1,3}\s*[0-9]{2,4}\s*$/;
const THIS_YEAR = new Date().getFullYear();

// Yeni araç penceresi. Açılınca kendini gösterir; kapanınca (kaydet, İptal ya da Esc) `closed` yayınlar.
@Component({
  imports: [ApiErrorAlert, ReactiveFormsModule],
  selector: 'app-vehicle-form',
  templateUrl: './vehicle-form.html',
})
export class VehicleForm {
  private readonly vehicleApi = inject(VehicleApi);
  private readonly live = inject(LiveVehicles);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly closed = output<void>();

  protected readonly types = VEHICLE_TYPES;
  protected readonly typeLabels = VEHICLE_TYPE_LABELS;
  protected readonly thisYear = THIS_YEAR;
  protected readonly driverLabel = driverLabel;

  // Sürücü listesi alınamazsa araç yine sürücüsüz eklenebilsin, sadece liste boş kalır
  protected readonly drivers = toSignal(inject(DriverApi).getDrivers().pipe(catchError(() => of([]))), {
    initialValue: [],
  });

  protected readonly form = inject(NonNullableFormBuilder).group({
    plate: ['', [Validators.required, Validators.pattern(PLATE_PATTERN)]],
    vehicleType: ['Otomobil' as VehicleType, Validators.required],
    brand: ['', [Validators.required, Validators.maxLength(50)]],
    model: ['', [Validators.required, Validators.maxLength(50)]],
    modelYear: [THIS_YEAR, [Validators.required, Validators.min(1990), Validators.max(THIS_YEAR + 1)]],
    speedLimitKmh: [DEFAULT_SPEED_LIMITS.Otomobil, [Validators.required, Validators.min(10), Validators.max(200)]],
    driverId: [null as number | null],
  });

  // Yazarken öneri: filodaki markalar ve seçili markanın modelleri ("Ford" ile "ford" ayrı yazılmasın diye)
  private readonly brand = toSignal(this.form.controls.brand.valueChanges, { initialValue: '' });
  protected readonly brands = computed(() => unique((this.live.vehicles() ?? []).map((v) => v.brand)));
  protected readonly models = computed(() =>
    unique(
      (this.live.vehicles() ?? [])
        .filter((v) => searchKey(v.brand) === searchKey(this.brand()))
        .map((v) => v.model),
    ),
  );

  protected readonly saving = signal(false);
  protected readonly saveError = signal<HttpErrorResponse | null>(null);

  constructor() {
    afterNextRender(() => this.dialog().nativeElement.showModal());

    // Tür seçilince hız sınırı o türün önerisi olur (sonra elle değiştirilebilir)
    this.form.controls.vehicleType.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((type) => this.form.controls.speedLimitKmh.setValue(DEFAULT_SPEED_LIMITS[type]));
  }

  // Alan dokunulmuş ve geçersizse hata yazısı gösterilir (boş formu açar açmaz kırmızı görmesin)
  protected invalid(name: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[name];
    return control.invalid && control.touched;
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.saveError.set(null);
    this.vehicleApi
      .createVehicle(this.form.getRawValue())
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.live.refresh();
          this.close();
        },
        error: (error: HttpErrorResponse) => this.saveError.set(error),
      });
  }

  protected close(): void {
    this.dialog().nativeElement.close(); // pencerenin 'close' olayı da `closed`ı yayınlar
  }
}

function unique(values: string[]): string[] {
  return [...new Set(values)].sort((a, b) => a.localeCompare(b, 'tr'));
}
