import type { Routes } from '@angular/router';
import { AuthCallbackComponent } from './core/auth/auth-callback.component';
import { authGuard, permissionGuard } from './core/auth/auth.guard';
import { HomePlaceholderComponent } from './shared/layout/home-placeholder.component';

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
  {
    path: 'broker/listings/new',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/broker/pages/broker-listing-editor.component').then(
        (m) => m.BrokerListingEditorComponent,
      ),
  },
  {
    path: 'broker/listings/:id/edit',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/broker/pages/broker-listing-editor.component').then(
        (m) => m.BrokerListingEditorComponent,
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
  { path: '', component: HomePlaceholderComponent },
];
