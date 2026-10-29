import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { LiveAlarms } from './services/live-alarms';

// Ana bileşen: üst menü + sayfaların çizildiği alan
@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  protected readonly unreadAlarms = inject(LiveAlarms).unreadCount;
}
