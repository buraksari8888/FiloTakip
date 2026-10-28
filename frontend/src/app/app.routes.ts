import { Routes } from '@angular/router';
import { VehicleList } from './pages/vehicle-list/vehicle-list';

export const routes: Routes = [
  { path: 'araclar', component: VehicleList, title: 'Araçlar · FiloTakip' },
  { path: '', redirectTo: 'araclar', pathMatch: 'full' },
];
