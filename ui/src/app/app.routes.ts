import type { Routes } from '@angular/router';
import { AuthCallbackComponent } from './core/auth/auth-callback.component';
import { authGuard, permissionGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'callback', component: AuthCallbackComponent },
  {
    path: 'broker/listings',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/broker/pages/broker-listings-page.component').then(
        (m) => m.BrokerListingsPageComponent,
      ),
  },
  {
    path: 'search',
    loadComponent: () =>
      import('./features/showcase/pages/search-page.component').then((m) => m.SearchPageComponent),
  },
  {
    path: 'listings/:id',
    loadComponent: () =>
      import('./features/showcase/pages/listing-detail-page.component').then(
        (m) => m.ListingDetailPageComponent,
      ),
  },
  // Deep links to the editor: the list page opens the create/edit dialog from this data.
  {
    path: 'broker/listings/new',
    canActivate: [authGuard, permissionGuard('listings:create')],
    data: { editor: 'new' },
    loadComponent: () =>
      import('./features/broker/pages/broker-listings-page.component').then(
        (m) => m.BrokerListingsPageComponent,
      ),
  },
  {
    path: 'broker/listings/:id/edit',
    canActivate: [authGuard],
    data: { editor: 'edit' },
    loadComponent: () =>
      import('./features/broker/pages/broker-listings-page.component').then(
        (m) => m.BrokerListingsPageComponent,
      ),
  },
  {
    path: 'admin/commission-rates',
    canActivate: [permissionGuard('commission:rates:manage')],
    loadComponent: () =>
      import('./features/admin/pages/commission-rates-admin-page.component').then(
        (m) => m.CommissionRatesAdminPageComponent,
      ),
  },
  {
    path: '',
    loadComponent: () =>
      import('./shared/layout/home-placeholder.component').then((m) => m.HomePlaceholderComponent),
  },
];
