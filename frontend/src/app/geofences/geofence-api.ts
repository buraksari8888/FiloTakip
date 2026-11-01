import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Geofence } from './geofence';

@Service()
export class GeofenceApi {
  private readonly http = inject(HttpClient);

  // Aktif bölgeler (haritada çizilir)
  getGeofences(): Observable<Geofence[]> {
    return this.http.get<Geofence[]>(`${environment.apiUrl}/geofences`);
  }
}
