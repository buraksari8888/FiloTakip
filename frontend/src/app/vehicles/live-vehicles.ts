import { inject, Service, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, EMPTY, switchMap, tap, timer } from 'rxjs';
import { VehicleApi } from './vehicle-api';

const REFRESH_MS = 3000; // simülatör de 3 saniyede bir konum gönderiyor

// Araçların canlı son konumları. Liste ve harita sayfası aynı veriyi buradan okur,
// böylece sayfalar arasında geçerken veri baştan yüklenmez.
@Service()
export class LiveVehicles {
  private readonly api = inject(VehicleApi);

  readonly apiError = signal(false);

  // Hemen ve sonra her 3 saniyede bir API'den son konumları çek.
  // API'ye ulaşılamazsa son gelen liste kalır, apiError true olur.
  readonly vehicles = toSignal(
    timer(0, REFRESH_MS).pipe(
      switchMap(() =>
        this.api.getLastLocations().pipe(
          tap(() => this.apiError.set(false)),
          catchError(() => {
            this.apiError.set(true);
            return EMPTY;
          }),
        ),
      ),
    ),
    { initialValue: null }, // null = henüz ilk cevap gelmedi
  );
}
