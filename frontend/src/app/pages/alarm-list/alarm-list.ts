import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, combineLatest, EMPTY, merge, Subject, switchMap, tap, timer } from 'rxjs';
import { Alarm, ALARM_TYPE_LABELS } from '../../models/alarm';
import { AlarmApi } from '../../services/alarm-api';
import { LiveAlarms } from '../../services/live-alarms';

const REFRESH_MS = 3000;

@Component({
  imports: [DatePipe],
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
  protected readonly apiError = signal(false);
  protected readonly typeLabels = ALARM_TYPE_LABELS;

  // Liste şu durumlarda yeniden çekilir: her 3 saniyede, filtre değişince, okundu işaretleyince
  protected readonly alarms = toSignal(
    combineLatest([toObservable(this.unreadOnly), merge(timer(0, REFRESH_MS), this.reloadNow)]).pipe(
      switchMap(([unreadOnly]) =>
        this.api.getAlarms(unreadOnly).pipe(
          tap(() => this.apiError.set(false)),
          catchError(() => {
            this.apiError.set(true);
            return EMPTY;
          }),
        ),
      ),
    ),
    { initialValue: null },
  );

  protected markAsRead(alarm: Alarm): void {
    this.api.markAsRead(alarm.alarmId).subscribe({
      next: () => this.reloadAll(),
      error: () => this.apiError.set(true),
    });
  }

  protected markAllAsRead(): void {
    this.api.markAllAsRead().subscribe({
      next: () => this.reloadAll(),
      error: () => this.apiError.set(true),
    });
  }

  // Hem bu listeyi hem üst menüdeki rozeti hemen güncelle
  private reloadAll(): void {
    this.reloadNow.next();
    this.liveAlarms.refresh();
  }
}
