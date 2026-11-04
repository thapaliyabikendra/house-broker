import type { HttpInterceptorFn } from '@angular/common/http';
import { HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import type { ProblemDetails } from './api-error';
import { ApiError } from './api-error';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }

      const isTokenEndpoint = req.url.includes('/connect/token');
      if (error.status === 401 && !isTokenEndpoint && auth.getRefreshToken() !== null) {
        return from(auth.refreshToken()).pipe(
          switchMap((refreshed) => {
            if (!refreshed) {
              auth.signOut(false);
              void auth.signIn(undefined, `${window.location.pathname}${window.location.search}`);
              return throwError(() => toApiError(error));
            }
            const retryReq = req.clone({
              setHeaders: { Authorization: `Bearer ${auth.getAccessToken() ?? ''}` },
            });
            return next(retryReq);
          }),
        );
      }

      return throwError(() => toApiError(error));
    }),
  );
};

function toApiError(error: HttpErrorResponse): ApiError {
  const problem = error.error as ProblemDetails | null;
  const correlationId =
    error.headers.get('X-Correlation-Id') ?? problem?.correlationId ?? undefined;
  const fieldErrors = problem?.errors ?? undefined;
  return new ApiError(
    error.status,
    problem?.code ?? undefined,
    correlationId,
    fieldErrors,
    problem?.title ?? problem?.detail ?? error.message,
  );
}
