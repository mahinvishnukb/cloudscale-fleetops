import { Routes } from '@angular/router';

import { authGuard } from './core/auth.guard';

/**
 * Every feature is lazy-loaded. The login screen is the only thing in the initial
 * bundle, which keeps first paint fast on a cold Render container.
 */
export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in · FleetOps',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'dashboard',
    title: 'Fleet overview · FleetOps',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
  },
  {
    path: 'fleet',
    title: 'Fleet · FleetOps',
    canActivate: [authGuard],
    loadComponent: () => import('./features/fleet/fleet.component').then((m) => m.FleetComponent),
  },
  {
    path: 'vessels/:id',
    title: 'Vessel · FleetOps',
    canActivate: [authGuard],
    loadComponent: () => import('./features/vessel/vessel.component').then((m) => m.VesselComponent),
  },
  {
    path: 'manifests',
    title: 'Manifests · FleetOps',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/manifests/manifests.component').then((m) => m.ManifestsComponent),
  },
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  { path: '**', redirectTo: 'dashboard' },
];
