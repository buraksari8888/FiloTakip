import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Alarm } from './alarm';

// Alarm endpoint'leriyle konuşan servis
@Service()
export class AlarmApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/alarms`;

  getAlarms(unreadOnly: boolean): Observable<Alarm[]> {
    return this.http.get<Alarm[]>(this.baseUrl, { params: { unreadOnly } });
  }

  // Bir aracın iki an arasında oluşan alarmları (geçmiş rota oynatma; en fazla 200, en yenisi önce)
  getVehicleAlarms(vehicleId: number, from: Date, to: Date): Observable<Alarm[]> {
    return this.http.get<Alarm[]>(this.baseUrl, {
      params: { vehicleId, from: from.toISOString(), to: to.toISOString(), take: 200 },
    });
  }

  getAlarm(alarmId: number): Observable<Alarm> {
    return this.http.get<Alarm>(`${this.baseUrl}/${alarmId}`);
  }

  getUnreadCount(): Observable<number> {
    return this.http.get<number>(`${this.baseUrl}/unread-count`);
  }

  markAsRead(alarmId: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${alarmId}/read`, null);
  }

  markAllAsRead(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/read-all`, null);
  }
}
