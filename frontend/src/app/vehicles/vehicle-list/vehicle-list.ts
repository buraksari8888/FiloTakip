import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiErrorAlert } from '../../shared/api-error-alert/api-error-alert';
import { LiveVehicles } from '../live-vehicles';
import { isLowFuel, isSpeeding, speedLimit, speedLimitSource } from '../vehicle-last-location';

@Component({
  imports: [ApiErrorAlert, DatePipe, DecimalPipe, RouterLink],
  selector: 'app-vehicle-list',
  styleUrl: './vehicle-list.scss',
  templateUrl: './vehicle-list.html',
})
export class VehicleList {
  private readonly live = inject(LiveVehicles);

  protected readonly vehicles = this.live.vehicles;
  protected readonly apiError = this.live.apiError;

  protected readonly speedingCount = computed(() => (this.vehicles() ?? []).filter(isSpeeding).length);
  protected readonly lowFuelCount = computed(() => (this.vehicles() ?? []).filter(isLowFuel).length);

  protected readonly isSpeeding = isSpeeding;
  protected readonly speedLimit = speedLimit;
  protected readonly speedLimitSource = speedLimitSource;
  protected readonly isLowFuel = isLowFuel;
}
