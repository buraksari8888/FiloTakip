import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { LocationUpdate } from '../core/live-hub';
import { environment } from '../../environments/environment';
import { VehicleLastLocation } from './vehicle-last-location';
import { NewVehicle } from './vehicle-type';

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

  // Yeni araç. Plaka pasife alınmış bir araçtaysa API o aracı yeniden açar.
  createVehicle(vehicle: NewVehicle): Observable<unknown> {
    return this.http.post(`${environment.apiUrl}/vehicles`, vehicle);
  }

  // Aracı pasife alır: listeden kalkar, geçmişi (konumlar, alarmlar) silinmez
  deactivateVehicle(vehicleId: number): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/vehicles/${vehicleId}`);
  }

  // Araca sürücü atar (null: sürücüsüz). Sürücü başka bir araçtaysa API onu oradan alır.
  assignDriver(vehicleId: number, driverId: number | null): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/vehicles/${vehicleId}/driver`, { driverId });
  }
}
