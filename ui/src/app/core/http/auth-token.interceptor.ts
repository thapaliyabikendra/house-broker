import type { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { RuntimeConfigService } from '../config/runtime-config.service';

/**
 * Attaches the bearer token to API calls only. An expired token is attached too;
 * the error interceptor refreshes it on the resulting 401.
 */
export const authTokenInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const apiBaseUrl = inject(RuntimeConfigService).value().apiBaseUrl;
  const isApiRequest = req.url === apiBaseUrl || req.url.startsWith(`${apiBaseUrl}/`);
  const token = auth.getAccessToken();

  if (isApiRequest && token) {
    req = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  }

  return next(req);
};
