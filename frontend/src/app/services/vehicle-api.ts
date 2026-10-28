import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { VehicleLastLocation } from '../models/vehicle-last-location';

const API_URL = 'http://localhost:5275/api';

// .NET API ile konuşan servis. Component'ler HTTP ayrıntısını bilmez, bu servisi kullanır.
@Service()
export class VehicleApi {
  private readonly http = inject(HttpClient);

  getLastLocations(): Observable<VehicleLastLocation[]> {
    return this.http.get<VehicleLastLocation[]>(`${API_URL}/vehicles/last-locations`);
  }
}
