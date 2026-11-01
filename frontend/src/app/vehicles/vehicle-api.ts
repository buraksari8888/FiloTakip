import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { LocationUpdate } from '../core/live-hub';
import { environment } from '../../environments/environment';
import { VehicleLastLocation } from './vehicle-last-location';

// .NET API ile konuşan servis. Component'ler HTTP ayrıntısını bilmez, bu servisi kullanır.
@Service()
export class VehicleApi {
  private readonly http = inject(HttpClient);

  getLastLocations(): Observable<VehicleLastLocation[]> {
    return this.http.get<VehicleLastLocation[]>(`${environment.apiUrl}/vehicles/last-locations`);
  }

  // Aracın son `minutes` dakikadaki konumları, eskiden yeniye (haritadaki iz için)
  getRecentLocations(vehicleId: number, minutes: number): Observable<LocationUpdate[]> {
    return this.http.get<LocationUpdate[]>(`${environment.apiUrl}/vehicles/${vehicleId}/locations`, {
      params: { minutes },
    });
  }

  // Aracın iki an arasındaki konumları, eskiden yeniye (geçmiş rota oynatma). Aralık en fazla 24 saat.
  getHistory(vehicleId: number, from: Date, to: Date): Observable<LocationUpdate[]> {
    return this.http.get<LocationUpdate[]>(`${environment.apiUrl}/vehicles/${vehicleId}/history`, {
      params: { from: from.toISOString(), to: to.toISOString() },
    });
  }
}
