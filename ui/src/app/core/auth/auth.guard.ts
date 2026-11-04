import { inject } from '@angular/core';
import type { CanActivateFn } from '@angular/router';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  if (auth.isAuthenticated()) {
    return true;
  }
  void auth.signIn();
  return false;
};

export function roleGuard(role: string): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.isAuthenticated()) {
      void auth.signIn();
      return false;
    }
    if (auth.hasRole(role) || auth.hasPermission(role)) {
      return true;
    }
    return router.createUrlTree(['/']);
  };
}

export function permissionGuard(permission: string): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.isAuthenticated()) {
      void auth.signIn();
      return false;
    }
    if (auth.hasPermission(permission)) {
      return true;
    }
    return router.createUrlTree(['/']);
  };
}
