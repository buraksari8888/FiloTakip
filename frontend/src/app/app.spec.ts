import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NEVER } from 'rxjs';
import { App } from './app';
import { LiveHub, LiveStatus } from './core/live-hub';

describe('App', () => {
  // Testte gerçek API'ye canlı bağlantı açılmasın: aynı alanlara sahip sahte bir bağlantı
  const status = signal<LiveStatus>('connected');

  beforeEach(async () => {
    status.set('connected');
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]), // üst menüdeki routerLink'ler router ister
        provideHttpClient(),
        provideHttpClientTesting(), // alarm rozeti API'ye istek atar; testte gerçek istek gitmesin
        { provide: LiveHub, useValue: { status, locationAdded$: NEVER, alarmsChanged$: NEVER, reconnected$: NEVER } },
      ],
    })
      .compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render brand in top bar', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand')?.textContent).toContain('FiloTakip');
  });

  it('should show the live connection status', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const live = (fixture.nativeElement as HTMLElement).querySelector('.live')!;
    expect(live.textContent).toContain('Canlı');
    expect(live.classList).toContain('connected');

    status.set('reconnecting');
    await fixture.whenStable();
    expect(live.textContent).toContain('Bağlantı yok');
    expect(live.classList).toContain('reconnecting');
  });
});
