import { Routes } from '@angular/router';
import { GraphPageComponent } from './graph-page/graph-page.component';

export const routes: Routes = [
  { path: '', component: GraphPageComponent },
  { path: '**', redirectTo: '' }
];
