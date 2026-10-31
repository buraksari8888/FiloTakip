import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { combineLatest, merge, retry, startWith, Subject, switchMap, tap, timer } from 'rxjs';
import { LiveHub, RETRY_MS } from '../../core/live-hub';
import { ApiErrorAlert } from '../../shared/api-error-alert/api-error-alert';
import { Alarm, ALARM_TYPE_LABELS } from '../alarm';
import { AlarmApi } from '../alarm-api';
import { LiveAlarms } from '../live-alarms';

@Component({
  imports: [ApiErrorAlert, DatePipe, RouterLink],
  selector: 'app-alarm-list',
  styleUrl: './alarm-list.scss',
  templateUrl: './alarm-list.html',
})
export class AlarmList {
  private readonly api = inject(AlarmApi);
  private readonly liveAlarms = inject(LiveAlarms);
  private readonly hub = inject(LiveHub);
  private readonly reloadNow = new Subject<void>();

  protected readonly unreadOnly = signal(false);
  protected readonly unreadCount = this.liveAlarms.unreadCount;
  protected readonly apiError = signal<HttpErrorResponse | null>(null);
  // Okundu işaretleme başarısız olursa hangi işlem olduğu ve hatası. Listenin hatasından ayrı tutulur:
  // aynı yerde olsaydı 3 saniye sonraki başarılı yenileme mesajı kullanıcı okumadan silerdi.
  protected readonly actionError = signal<{ action: string; error: HttpErrorResponse } | null>(null);
  protected readonly typeLabels = ALARM_TYPE_LABELS;

  // Liste şu durumlarda yeniden çekilir: sayfa açılınca, filtre değişince, okundu işaretleyince,
  // API "alarmlar değişti" deyince ve canlı bağlantı yeniden kurulunca. Başarısız olursa 5 saniyede bir denenir.
  private readonly reload = merge(this.hub.alarmsChanged$, this.hub.reconnected$, this.reloadNow).pipe(
    startWith(undefined),
  );

  protected readonly alarms = toSignal(
    combineLatest([toObservable(this.unreadOnly), this.reload]).pipe(
      switchMap(([unreadOnly]) =>
        this.api.getAlarms(unreadOnly).pipe(
          tap(() => this.apiError.set(null)),
          retry({
            delay: (error: HttpErrorResponse) => {
              this.apiError.set(error);
              return timer(RETRY_MS);
            },
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
