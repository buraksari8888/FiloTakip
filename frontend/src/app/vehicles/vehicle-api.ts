import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { VehicleLastLocation } from './vehicle-last-location';

// .NET API ile konuşan servis. Component'ler HTTP ayrıntısını bilmez, bu servisi kullanır.
@Service()
export class VehicleApi {
  private readonly http = inject(HttpClient);

  getLastLocations(): Observable<VehicleLastLocation[]> {
    return this.http.get<VehicleLastLocation[]>(`${environment.apiUrl}/vehicles/last-locations`);
  }
}
