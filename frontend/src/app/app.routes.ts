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
  { path: 'araclar', component: VehicleList, title: 'Araçlar · FiloTakip' },
  { path: 'alarmlar', component: AlarmList, title: 'Alarmlar · FiloTakip' },
  { path: '', redirectTo: 'harita', pathMatch: 'full' },
];
