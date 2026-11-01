import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { LiveAlarms } from './alarms/live-alarms';
import { LiveHub, LiveStatus } from './core/live-hub';
import { ThemeService } from './core/theme';

// Üst menüdeki canlı bağlantı göstergesinin yazısı ve açıklaması (fareyle üstüne gelince)
const LIVE_STATUS_TEXT: Record<LiveStatus, { label: string; title: string }> = {
  connecting: { label: 'Bağlanıyor…', title: 'Canlı bağlantı kuruluyor' },
  connected: { label: 'Canlı', title: 'Yeni konumlar ve alarmlar anında geliyor' },
  reconnecting: {
    label: 'Bağlantı yok',
    title: 'API ile canlı bağlantı yok, 5 saniyede bir yeniden deneniyor. Ekrandaki veriler eski olabilir.',
  },
};

// Ana bileşen: üst menü + sayfaların çizildiği alan
@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  protected readonly unreadAlarms = inject(LiveAlarms).unreadCount;
  protected readonly liveStatus = inject(LiveHub).status;
  protected readonly liveStatusText = LIVE_STATUS_TEXT;
  protected readonly theme = inject(ThemeService);
}
