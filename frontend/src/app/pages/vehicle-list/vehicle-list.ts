import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { isLowFuel, isSpeeding } from '../../models/vehicle-last-location';
import { LiveVehicles } from '../../services/live-vehicles';

@Component({
  imports: [DatePipe, DecimalPipe],
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
  protected readonly isLowFuel = isLowFuel;
}
