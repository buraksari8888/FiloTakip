import { Routes } from '@angular/router';
import { AlarmList } from './alarms/alarm-list/alarm-list';
import { VehicleMap } from './map/vehicle-map/vehicle-map';
import { VehicleList } from './vehicles/vehicle-list/vehicle-list';

export const routes: Routes = [
  { path: 'harita', component: VehicleMap, title: 'Harita · FiloTakip' },
  { path: 'araclar', component: VehicleList, title: 'Araçlar · FiloTakip' },
  { path: 'alarmlar', component: AlarmList, title: 'Alarmlar · FiloTakip' },
  { path: '', redirectTo: 'harita', pathMatch: 'full' },
];
