import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { Alarm } from '../models/alarm';

const API_URL = 'http://localhost:5275/api';

// Alarm endpoint'leriyle konuşan servis
@Service()
export class AlarmApi {
  private readonly http = inject(HttpClient);

  getAlarms(unreadOnly: boolean): Observable<Alarm[]> {
    return this.http.get<Alarm[]>(`${API_URL}/alarms`, { params: { unreadOnly } });
  }

  getUnreadCount(): Observable<number> {
    return this.http.get<number>(`${API_URL}/alarms/unread-count`);
  }

  markAsRead(alarmId: number): Observable<void> {
    return this.http.post<void>(`${API_URL}/alarms/${alarmId}/read`, null);
  }

  markAllAsRead(): Observable<void> {
    return this.http.post<void>(`${API_URL}/alarms/read-all`, null);
  }
}
