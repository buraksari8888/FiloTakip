import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, combineLatest, EMPTY, merge, Subject, switchMap, tap, timer } from 'rxjs';
import { ApiErrorAlert } from '../../shared/api-error-alert/api-error-alert';
import { Alarm, ALARM_TYPE_LABELS } from '../alarm';
import { AlarmApi } from '../alarm-api';
import { LiveAlarms } from '../live-alarms';

const REFRESH_MS = 3000;

@Component({
  imports: [ApiErrorAlert, DatePipe],
  selector: 'app-alarm-list',
  styleUrl: './alarm-list.scss',
  templateUrl: './alarm-list.html',
})
export class AlarmList {
  private readonly api = inject(AlarmApi);
  private readonly liveAlarms = inject(LiveAlarms);
  private readonly reloadNow = new Subject<void>();

  protected readonly unreadOnly = signal(false);
  protected readonly unreadCount = this.liveAlarms.unreadCount;
  protected readonly apiError = signal<HttpErrorResponse | null>(null);
  // Okundu işaretleme başarısız olursa hangi işlem olduğu ve hatası. Listenin hatasından ayrı tutulur:
  // aynı yerde olsaydı 3 saniye sonraki başarılı yenileme mesajı kullanıcı okumadan silerdi.
  protected readonly actionError = signal<{ action: string; error: HttpErrorResponse } | null>(null);
  protected readonly typeLabels = ALARM_TYPE_LABELS;

  // Liste şu durumlarda yeniden çekilir: her 3 saniyede, filtre değişince, okundu işaretleyince
  protected readonly alarms = toSignal(
    combineLatest([toObservable(this.unreadOnly), merge(timer(0, REFRESH_MS), this.reloadNow)]).pipe(
      switchMap(([unreadOnly]) =>
        this.api.getAlarms(unreadOnly).pipe(
          tap(() => this.apiError.set(null)),
          catchError((error: HttpErrorResponse) => {
            this.apiError.set(error);
            return EMPTY;
          }),
        ),
      ),
    ),
    { initialValue: null },
  );

  protected markAsRead(alarm: Alarm): void {
    this.actionError.set(null);
    this.api.markAsRead(alarm.alarmId).subscribe({
      next: () => this.reloadAll(),
      error: (error: HttpErrorResponse) =>
        this.actionError.set({ action: 'Alarm okundu işaretlenemedi', error }),
    });
  }

  protected markAllAsRead(): void {
    this.actionError.set(null);
    this.api.markAllAsRead().subscribe({
      next: () => this.reloadAll(),
      error: (error: HttpErrorResponse) =>
        this.actionError.set({ action: 'Alarmlar okundu işaretlenemedi', error }),
    });
  }

  // Hem bu listeyi hem üst menüdeki rozeti hemen güncelle
  private reloadAll(): void {
    this.reloadNow.next();
    this.liveAlarms.refresh();
  }
}
