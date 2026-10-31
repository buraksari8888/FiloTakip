import { registerLocaleData } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import localeTr from '@angular/common/locales/tr';
import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';

// Sayı ve tarihler Türkçe biçimde gösterilsin (71,2 / 28.10.2026)
registerLocaleData(localeTr);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // withComponentInputBinding: adresteki parametreler (ör. /harita?arac=3) sayfanın input'larına verilir
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(),
    { provide: LOCALE_ID, useValue: 'tr' },
  ]
};
