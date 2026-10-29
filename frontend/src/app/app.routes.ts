import { Routes } from '@angular/router';
import { AlarmList } from './pages/alarm-list/alarm-list';
import { VehicleList } from './pages/vehicle-list/vehicle-list';
import { VehicleMap } from './pages/vehicle-map/vehicle-map';

export const routes: Routes = [
  { path: 'harita', component: VehicleMap, title: 'Harita · FiloTakip' },
  { path: 'araclar', component: VehicleList, title: 'Araçlar · FiloTakip' },
  { path: 'alarmlar', component: AlarmList, title: 'Alarmlar · FiloTakip' },
  { path: '', redirectTo: 'harita', pathMatch: 'full' },
];
