import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, EMPTY, switchMap, tap, timer } from 'rxjs';
import { VehicleLastLocation } from '../../models/vehicle-last-location';
import { VehicleApi } from '../../services/vehicle-api';

const REFRESH_MS = 3000;   // simülatör de 3 saniyede bir konum gönderiyor
const LOW_FUEL_PERCENT = 15;

@Component({
  imports: [DatePipe, DecimalPipe],
  selector: 'app-vehicle-list',
  styleUrl: './vehicle-list.scss',
  templateUrl: './vehicle-list.html',
})
export class VehicleList {
  private readonly api = inject(VehicleApi);

  protected readonly apiError = signal(false);

  // Hemen ve sonra her 3 saniyede bir API'den son konumları çek.
  // API'ye ulaşılamazsa son gelen liste ekranda kalır, üstte uyarı çıkar.
  protected readonly vehicles = toSignal(
    timer(0, REFRESH_MS).pipe(
      switchMap(() =>
        this.api.getLastLocations().pipe(
          tap(() => this.apiError.set(false)),
          catchError(() => {
            this.apiError.set(true);
            return EMPTY;
          }),
        ),
      ),
    ),
    { initialValue: null }, // null = henüz ilk cevap gelmedi
  );

  protected readonly speedingCount = computed(() => (this.vehicles() ?? []).filter(isSpeeding).length);
  protected readonly lowFuelCount = computed(() => (this.vehicles() ?? []).filter(isLowFuel).length);

  protected readonly isSpeeding = isSpeeding;
  protected readonly isLowFuel = isLowFuel;
}

function isSpeeding(v: VehicleLastLocation): boolean {
  return v.speedKmh !== null && v.speedKmh > v.speedLimitKmh;
}

function isLowFuel(v: VehicleLastLocation): boolean {
  return v.fuelPercent !== null && v.fuelPercent < LOW_FUEL_PERCENT;
}
