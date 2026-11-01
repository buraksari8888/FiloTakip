import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Geofence, GeofenceInput } from './geofence';

@Service()
export class GeofenceApi {
  private readonly http = inject(HttpClient);

  // Aktif bölgeler (haritada çizilir)
  getGeofences(): Observable<Geofence[]> {
    return this.http.get<Geofence[]>(`${environment.apiUrl}/geofences`);
  }

  // Yeni bölge. Aynı adda bölge varsa API 409 döner.
  createGeofence(geofence: GeofenceInput): Observable<Geofence> {
    return this.http.post<Geofence>(`${environment.apiUrl}/geofences`, geofence);
  }

  // Bölgenin adı, yeri, yarıçapı ya da alarm ayarı değişir
  updateGeofence(geofenceId: number, geofence: GeofenceInput): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/geofences/${geofenceId}`, geofence);
  }

  // Bölgeyi siler (pasife alır): artık alarm üretmez; daha önce üretilen alarmlar silinmez
  deleteGeofence(geofenceId: number): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/geofences/${geofenceId}`);
  }
}
