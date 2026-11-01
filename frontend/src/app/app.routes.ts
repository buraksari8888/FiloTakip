import { Routes } from '@angular/router';
import { AlarmList } from './alarms/alarm-list/alarm-list';
import { VehicleList } from './vehicles/vehicle-list/vehicle-list';

export const routes: Routes = [
  {
    path: 'harita',
    // Harita sayfası ve kullandığı Leaflet (~146 kB) ayrı bir dosyada: sadece bu sayfa açılınca indirilir.
    // Diğer sayfalar küçük (hepsi ~15 kB), onları ayırmaya değmez.
    loadComponent: () => import('./map/vehicle-map/vehicle-map').then((m) => m.VehicleMap),
    title: 'Harita · FiloTakip',
  },
  {
    path: 'gecmis',
    // Harita sayfası gibi Leaflet kullanır, o yüzden o da ayrı dosyada
    loadComponent: () => import('./map/route-playback/route-playback').then((m) => m.RoutePlayback),
    title: 'Geçmiş rota · FiloTakip',
  },
  { path: 'araclar', component: VehicleList, title: 'Araçlar · FiloTakip' },
  {
    path: 'bolgeler',
    // Harita sayfası gibi Leaflet kullanır, o yüzden o da ayrı dosyada
    loadComponent: () =>
      import('./geofences/geofence-manager/geofence-manager').then((m) => m.GeofenceManager),
    title: 'Bölgeler · FiloTakip',
  },
  { path: 'alarmlar', component: AlarmList, title: 'Alarmlar · FiloTakip' },
  { path: '', redirectTo: 'harita', pathMatch: 'full' },
  // Bilinmeyen adres (/bilinmeyen-sayfa, yanlış yazılmış bağlantı): boş sayfa ve hata yerine haritaya git
  { path: '**', redirectTo: 'harita' },
];
