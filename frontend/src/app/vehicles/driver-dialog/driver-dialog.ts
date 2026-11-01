import { HttpErrorResponse } from '@angular/common/http';
import { afterNextRender, Component, computed, ElementRef, inject, input, output, signal, viewChild } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, Observable, switchMap } from 'rxjs';
import { Driver, driverLabel } from '../../drivers/driver';
import { DriverApi } from '../../drivers/driver-api';
import { ApiErrorAlert } from '../../shared/api-error-alert/api-error-alert';
import { LiveVehicles } from '../live-vehicles';
import { VehicleApi } from '../vehicle-api';
import { VehicleLastLocation } from '../vehicle-last-location';

// Sürücü penceresi, iki ayrı işlem:
// - Adı düzelt: sürücünün adındaki yazım hatası (aynı kişi; sürücünün kaydı güncellenir)
// - Sürücüyü değiştir: araç başka birine verildi (mevcut bir sürücü, yeni sürücü ya da sürücüsüz)
@Component({
  imports: [ApiErrorAlert, ReactiveFormsModule],
  selector: 'app-driver-dialog',
  templateUrl: './driver-dialog.html',
})
export class DriverDialog {
  private readonly driverApi = inject(DriverApi);
  private readonly vehicleApi = inject(VehicleApi);
  private readonly live = inject(LiveVehicles);
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  readonly vehicle = input.required<VehicleLastLocation>();
  readonly closed = output<void>();

  protected readonly driverLabel = driverLabel;
  protected readonly drivers = signal<Driver[] | null>(null); // null = yükleniyor
  protected readonly currentDriver = computed(
    () => this.drivers()?.find((d) => d.driverId === this.vehicle().driverId) ?? null,
  );
  // Değiştirirken seçilebilecekler: aracın şu anki sürücüsü hariç herkes
  protected readonly otherDrivers = computed(() =>
    (this.drivers() ?? []).filter((d) => d.driverId !== this.vehicle().driverId),
  );

  protected readonly nameForm = this.fb.group({
    firstName: ['', [Validators.required, Validators.maxLength(50)]],
    lastName: ['', [Validators.required, Validators.maxLength(50)]],
  });

  // Sürücü değiştirme seçimi: 'none' = sürücüsüz, 'new' = yeni sürücü, sayı = mevcut sürücünün numarası
  protected readonly choice = signal<string>('');
  protected readonly newDriverForm = this.fb.group({
    firstName: ['', [Validators.required, Validators.maxLength(50)]],
    lastName: ['', [Validators.required, Validators.maxLength(50)]],
    licenseNumber: ['', [Validators.required, Validators.maxLength(20)]],
    phone: ['', Validators.maxLength(20)],
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<{ action: string; error: HttpErrorResponse } | null>(null);

  constructor() {
    afterNextRender(() => this.dialog().nativeElement.showModal());

    this.driverApi.getDrivers().subscribe({
      next: (drivers) => {
        this.drivers.set(drivers);
        const current = this.currentDriver();
        if (current) {
          this.nameForm.setValue({ firstName: current.firstName, lastName: current.lastName });
        }
      },
      error: (error: HttpErrorResponse) => this.error.set({ action: 'Sürücüler yüklenemedi', error }),
    });
  }

  protected saveName(): void {
    const current = this.currentDriver();
    if (!current || this.nameForm.invalid) {
      this.nameForm.markAllAsTouched();
      return;
    }
    const { firstName, lastName } = this.nameForm.getRawValue();
    this.run('Ad düzeltilemedi', this.driverApi.updateName(current.driverId, firstName, lastName));
  }

  protected saveAssignment(): void {
    const vehicleId = this.vehicle().vehicleId;
    const choice = this.choice();
    if (choice === 'new') {
      if (this.newDriverForm.invalid) {
        this.newDriverForm.markAllAsTouched();
        return;
      }
      const { phone, ...driver } = this.newDriverForm.getRawValue();
      // Önce sürücü eklenir, API'nin verdiği numarayla araca atanır
      this.run(
        'Sürücü eklenemedi',
        this.driverApi
          .createDriver({ ...driver, phone: phone.trim() || null })
          .pipe(switchMap((created) => this.vehicleApi.assignDriver(vehicleId, created.driverId))),
      );
    } else if (choice !== '') {
      this.run('Sürücü değiştirilemedi', this.vehicleApi.assignDriver(vehicleId, choice === 'none' ? null : +choice));
    }
  }

  // Yeni sürücü alanı dokunulmuş ve geçersizse hata yazısı gösterilir
  protected invalid(name: keyof typeof this.newDriverForm.controls): boolean {
    const control = this.newDriverForm.controls[name];
    return control.invalid && control.touched;
  }

  protected close(): void {
    this.dialog().nativeElement.close(); // pencerenin 'close' olayı da `closed`ı yayınlar
  }

  // İsteği gönderir; başarılıysa araç listesini yenileyip pencereyi kapatır, değilse hatayı gösterir
  private run(action: string, request: Observable<unknown>): void {
    this.saving.set(true);
    this.error.set(null);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.live.refresh();
        this.close();
      },
      error: (error: HttpErrorResponse) => this.error.set({ action, error }),
    });
  }
}
