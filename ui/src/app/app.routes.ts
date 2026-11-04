import type { Routes } from '@angular/router';
import { AuthCallbackComponent } from './core/auth/auth-callback.component';
import { HomePlaceholderComponent } from './shared/layout/home-placeholder.component';

export const routes: Routes = [
  { path: 'auth/callback', component: AuthCallbackComponent },
  { path: '', component: HomePlaceholderComponent },
];
