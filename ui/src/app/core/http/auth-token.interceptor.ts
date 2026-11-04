import type { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { RuntimeConfigService } from '../config/runtime-config.service';

export const authTokenInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const apiBaseUrl = inject(RuntimeConfigService).value().apiBaseUrl;
  const isApiRequest = req.url.startsWith(apiBaseUrl) || req.url.includes('/api/');
  const token = auth.getAccessToken();

  if (isApiRequest && token && !auth.isTokenExpired()) {
    req = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  } else if (isApiRequest && token) {
    // expired token will be handled by refresh in the error interceptor
    req = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  }

  return next(req);
};
