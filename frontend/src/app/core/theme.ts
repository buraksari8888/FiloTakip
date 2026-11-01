import { effect, Service, signal } from '@angular/core';

export type Theme = 'dark' | 'light';

// index.html'deki küçük betik de bu anahtarı okur (sayfa açılırken yanlış tema bir an görünmesin diye)
const STORAGE_KEY = 'filotakip-theme';

// Koyu / açık tema. Seçim tarayıcıda saklanır, sonraki açılışta hatırlanır.
// Renkler styles.scss'te; burada sadece <html data-theme="..."> değiştirilir.
@Service()
export class ThemeService {
  readonly theme = signal<Theme>(readSavedTheme());

  constructor() {
    effect(() => {
      const theme = this.theme();
      document.documentElement.dataset['theme'] = theme;
      try {
        localStorage.setItem(STORAGE_KEY, theme);
      } catch {
        // Gizli pencere ya da tarayıcı ayarı saklamaya izin vermiyor: tema bu oturumda yine çalışır
      }
    });
  }

  toggle(): void {
    this.theme.update((theme) => (theme === 'dark' ? 'light' : 'dark'));
  }
}

function readSavedTheme(): Theme {
  try {
    return localStorage.getItem(STORAGE_KEY) === 'light' ? 'light' : 'dark';
  } catch {
    return 'dark';
  }
}
