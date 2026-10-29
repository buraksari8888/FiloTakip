import { inject, Service } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, EMPTY, merge, Subject, switchMap, timer } from 'rxjs';
import { AlarmApi } from './alarm-api';

const REFRESH_MS = 3000;

// Okunmamış alarm sayısı (üst menüdeki rozet). Her sayfada görünür, 3 saniyede bir güncellenir.
@Service()
export class LiveAlarms {
  private readonly api = inject(AlarmApi);
  private readonly refreshNow = new Subject<void>();

  // Hem zamanlayıcı her "tık"ladığında hem de refresh() çağrıldığında sayıyı yeniden çek
  readonly unreadCount = toSignal(
    merge(timer(0, REFRESH_MS), this.refreshNow).pipe(
      switchMap(() => this.api.getUnreadCount().pipe(catchError(() => EMPTY))),
    ),
    { initialValue: 0 },
  );

  // Bir alarm okundu işaretlenince rozeti 3 saniye beklemeden güncellemek için
  refresh(): void {
    this.refreshNow.next();
  }
}
