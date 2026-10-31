import { inject, Service } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { merge, retry, startWith, Subject, switchMap, timer } from 'rxjs';
import { LiveHub, RETRY_MS } from '../core/live-hub';
import { AlarmApi } from './alarm-api';

// Okunmamış alarm sayısı (üst menüdeki rozet). Her sayfada görünür.
@Service()
export class LiveAlarms {
  private readonly api = inject(AlarmApi);
  private readonly hub = inject(LiveHub);
  private readonly refreshNow = new Subject<void>();

  // Sayı şu durumlarda yeniden çekilir: açılışta, API "alarmlar değişti" deyince, canlı bağlantı yeniden
  // kurulunca ve refresh() çağrılınca. İstek başarısız olursa 5 saniyede bir yeniden denenir.
  readonly unreadCount = toSignal(
    merge(this.hub.alarmsChanged$, this.hub.reconnected$, this.refreshNow).pipe(
      startWith(undefined),
      switchMap(() => this.api.getUnreadCount().pipe(retry({ delay: () => timer(RETRY_MS) }))),
    ),
    { initialValue: 0 },
  );

  // Bu sekmede bir alarm okundu işaretlenince rozeti hemen güncellemek için
  // (canlı bağlantı o an kopuk olsa bile çalışsın)
  refresh(): void {
    this.refreshNow.next();
  }
}
