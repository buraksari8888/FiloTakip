import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, input } from '@angular/core';

// API hatası uyarı kutusu. Hatanın türüne göre doğru mesajı gösterir (API kapalı mı, sunucuda mı hata var...).
// Araç listesi, alarm listesi ve harita sayfası bunu kullanır.
@Component({
  selector: 'app-api-error-alert',
  styleUrl: './api-error-alert.scss',
  templateUrl: './api-error-alert.html',
})
export class ApiErrorAlert {
  readonly error = input.required<HttpErrorResponse>();
  // Başarısız olan işlem (ör. "Alarm okundu işaretlenemedi"). Verilmezse sayfanın verisi alınamadı demektir.
  readonly action = input<string>();

  // status 0 = sunucudan hiç cevap gelmedi (API kapalı ya da adres yanlış)
  protected readonly unreachable = computed(() => this.error().status === 0);

  protected readonly message = computed(() => {
    const status = this.error().status;
    if (status === 0) {
      return "API'ye ulaşılamıyor. API çalışıyor mu?";
    }
    if (status === 404) {
      return 'İstenen kayıt bulunamadı.';
    }
    if (status === 409) {
      return 'Bu kayıt zaten var.';
    }
    if (status >= 500) {
      return 'Sunucuda bir hata oluştu.';
    }
    return 'İstek geçersiz.';
  });

  // API'nin kendi açıklaması: doğrulama hataları ("Plaka geçersiz...") ya da çakışmada (409) başlık
  protected readonly details = computed(() => {
    const body = this.error().error;
    if (body?.errors && typeof body.errors === 'object') {
      return Object.values(body.errors as Record<string, unknown>)
        .flat()
        .filter((message): message is string => typeof message === 'string');
    }
    if (this.error().status === 409 && typeof body?.title === 'string') {
      return [body.title];
    }
    return [];
  });

  // API'nin hata cevabındaki (ProblemDetails) iz numarası. Sorun bildirilirken bu numara verilirse
  // hatanın ayrıntısı API'nin logunda bulunur.
  protected readonly traceId = computed(() => {
    const traceId = this.error().error?.traceId;
    return typeof traceId === 'string' ? traceId : null;
  });
}
