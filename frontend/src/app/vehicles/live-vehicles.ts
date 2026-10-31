import { HttpErrorResponse } from '@angular/common/http';
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

  // Son isteğin hatası (başarılıysa null). Sayfalar bunu uyarı kutusunda gösterir.
  readonly apiError = signal<HttpErrorResponse | null>(null);

  // Hemen ve sonra her 3 saniyede bir API'den son konumları çek.
  // İstek başarısız olursa son gelen liste kalır, hata apiError'a yazılır.
  readonly vehicles = toSignal(
    timer(0, REFRESH_MS).pipe(
      switchMap(() =>
        this.api.getLastLocations().pipe(
          tap(() => this.apiError.set(null)),
          catchError((error: HttpErrorResponse) => {
            this.apiError.set(error);
            return EMPTY;
          }),
        ),
      ),
    ),
    { initialValue: null }, // null = henüz ilk cevap gelmedi
  );
}
