import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiErrorAlert } from '../../shared/api-error-alert/api-error-alert';
import { DriverDialog } from '../driver-dialog/driver-dialog';
import { LiveVehicles } from '../live-vehicles';
import { VehicleApi } from '../vehicle-api';
import { VehicleForm } from '../vehicle-form/vehicle-form';
import { isLowFuel, isSpeeding, speedLimit, speedLimitSource, VehicleLastLocation } from '../vehicle-last-location';
import { matchesSearch } from '../vehicle-search';
import { VEHICLE_TYPE_LABELS } from '../vehicle-type';

@Component({
  imports: [ApiErrorAlert, DatePipe, DecimalPipe, DriverDialog, RouterLink, VehicleForm],
  selector: 'app-vehicle-list',
  styleUrl: './vehicle-list.scss',
  templateUrl: './vehicle-list.html',
})
export class VehicleList {
  private readonly live = inject(LiveVehicles);
  private readonly api = inject(VehicleApi);

  protected readonly vehicles = this.live.vehicles;
  protected readonly apiError = this.live.apiError;
  // Pasife alma başarısız olursa. Listenin hatasından ayrı: liste yenilenince silinmesin.
  protected readonly actionError = signal<{ action: string; error: HttpErrorResponse } | null>(null);

  // Arama: plaka, marka, model, tür ya da sürücü adı (büyük/küçük harf ve Türkçe harf önemsiz)
  protected readonly query = signal('');
  protected readonly filtered = computed(() => (this.vehicles() ?? []).filter((v) => matchesSearch(v, this.query())));

  protected readonly speedingCount = computed(() => (this.vehicles() ?? []).filter(isSpeeding).length);
  protected readonly lowFuelCount = computed(() => (this.vehicles() ?? []).filter(isLowFuel).length);

  // Açık pencere: yeni araç ya da hangi aracın sürücüsü düzenleniyor (null = kapalı).
  // Araç, tıklandığı andaki haliyle saklanır: canlı konum geldikçe pencere baştan çizilmesin.
  protected readonly adding = signal(false);
  protected readonly driverFor = signal<VehicleLastLocation | null>(null);

  protected readonly isSpeeding = isSpeeding;
  protected readonly speedLimit = speedLimit;
  protected readonly speedLimitSource = speedLimitSource;
  protected readonly isLowFuel = isLowFuel;
  protected readonly typeLabels = VEHICLE_TYPE_LABELS;

  protected deactivate(v: VehicleLastLocation): void {
    const question =
      `${v.plate} listeden çıkarılsın mı?\n\n` +
      'Araç haritadan ve listeden kalkar, konum gönderemez. Geçmişi (konumlar, alarmlar) silinmez; ' +
      'aynı plaka tekrar eklenirse geçmişiyle birlikte geri gelir.';
    if (!confirm(question)) {
      return;
    }
    this.actionError.set(null);
    this.api.deactivateVehicle(v.vehicleId).subscribe({
      next: () => this.live.refresh(),
      error: (error: HttpErrorResponse) => this.actionError.set({ action: `${v.plate} çıkarılamadı`, error }),
    });
  }
}
