import { HttpErrorResponse } from '@angular/common/http';
import { inject, Service, signal } from '@angular/core';
import { merge, retry, startWith, Subject, switchMap, tap, timer } from 'rxjs';
import { LiveHub, RETRY_MS } from '../core/live-hub';
import { VehicleApi } from './vehicle-api';
import { applyLocation, VehicleLastLocation } from './vehicle-last-location';

// Araçların canlı son konumları. Liste ve harita sayfası aynı veriyi buradan okur,
// böylece sayfalar arasında geçerken veri baştan yüklenmez.
@Service()
export class LiveVehicles {
  private readonly api = inject(VehicleApi);
  private readonly hub = inject(LiveHub);
  private readonly list = signal<VehicleLastLocation[] | null>(null); // null = henüz ilk cevap gelmedi
  private readonly refreshNow = new Subject<void>();

  readonly vehicles = this.list.asReadonly();
  // Son isteğin hatası (başarılıysa null). Sayfalar bunu uyarı kutusunda gösterir.
  readonly apiError = signal<HttpErrorResponse | null>(null);

  constructor() {
    // Listenin tamamı şu durumlarda çekilir: açılışta, canlı bağlantı koptuktan sonra yeniden kurulunca
    // (kopukken gelen konumlar kaçmış olabilir), API "araçlar değişti" deyince ve refresh() çağrılınca.
    // İstek başarısız olursa 5 saniyede bir yeniden denenir.
    merge(this.hub.reconnected$, this.hub.vehiclesChanged$, this.refreshNow)
      .pipe(
        startWith(undefined),
        switchMap(() =>
          this.api.getLastLocations().pipe(
            tap(() => this.apiError.set(null)),
            retry({
              delay: (error: HttpErrorResponse) => {
                this.apiError.set(error);
                return timer(RETRY_MS);
              },
            }),
          ),
        ),
      )
      .subscribe((list) => this.list.set(list));

    // Arada yeni konum gelince sadece o aracın satırı güncellenir, liste baştan çekilmez
    this.hub.locationAdded$.subscribe((location) =>
      this.list.update((list) => list && applyLocation(list, location)),
    );
  }

  // Bu sekmede araç eklenince/çıkarılınca listeyi hemen yenilemek için (canlı bağlantı o an kopuk olsa bile)
  refresh(): void {
    this.refreshNow.next();
  }
}
