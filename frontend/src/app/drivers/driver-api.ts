import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Driver, NewDriver } from './driver';

// Sürücü endpoint'leriyle konuşan servis
@Service()
export class DriverApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/drivers`;

  getDrivers(): Observable<Driver[]> {
    return this.http.get<Driver[]>(this.baseUrl);
  }

  createDriver(driver: NewDriver): Observable<Driver> {
    return this.http.post<Driver>(this.baseUrl, driver);
  }

  // Adın yazımını düzeltmek için (araç başka birine verildiyse ad değil sürücü değiştirilir)
  updateName(driverId: number, firstName: string, lastName: string): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${driverId}`, { firstName, lastName });
  }
}
