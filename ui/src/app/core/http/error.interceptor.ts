import type { HttpInterceptorFn } from '@angular/common/http';
import { HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import type { ProblemDetails } from './api-error';
import { ApiError } from './api-error';
import { TranslocoService } from '@jsverse/transloco';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const transloco = inject(TranslocoService, { optional: true });

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
              return throwError(() => toApiError(error, transloco));
            }
            const retryReq = req.clone({
              setHeaders: { Authorization: `Bearer ${auth.getAccessToken() ?? ''}` },
            });
            return next(retryReq);
          }),
        );
      }

      return throwError(() => toApiError(error, transloco));
    }),
  );
};

function toApiError(error: HttpErrorResponse, transloco?: TranslocoService | null): ApiError {
  const problem = error.error as ProblemDetails | null;
  const correlationId =
    error.headers.get('X-Correlation-Id') ?? problem?.correlationId ?? undefined;
  const fieldErrors = problem?.errors ?? undefined;

  let message = problem?.title ?? problem?.detail ?? undefined;
  if (!message) {
    const translate = (key: string, fallback: string): string => {
      const value = transloco?.translate(key);
      return value && value !== key ? value : fallback;
    };
    if (error.status === 0) {
      message = translate(
        'errors.network',
        'Network error. Please check your connection and try again.',
      );
    } else if (error.status === 401) {
      message = translate('errors.sessionExpired', 'Your session expired. Please sign in again.');
    } else if (error.status === 403) {
      message = translate('errors.forbidden', 'You are not allowed to perform this action.');
    } else if (error.status === 404) {
      message = translate('errors.notFound', 'The requested resource was not found.');
    } else if (error.status === 409) {
      message = translate(
        'errors.conflict',
        'The request conflicts with the current state. Please refresh and try again.',
      );
    } else if (error.status === 412) {
      message = translate(
        'errors.stale',
        'This record was changed by someone else. Please refresh and try again.',
      );
    } else if (error.status === 422) {
      message = translate(
        'errors.validation',
        'Please correct the highlighted fields and try again.',
      );
    } else if (error.status === 428) {
      message = translate(
        'errors.preconditionRequired',
        'A version (If-Match) is required for this change.',
      );
    } else if (error.status >= 500) {
      message = translate(
        'errors.server',
        'Something went wrong on the server. Please try again later.',
      );
    } else {
      message = translate('errors.unexpected', error.message || 'Unexpected error.');
    }
  }

  return new ApiError(
    error.status,
    problem?.code ?? undefined,
    correlationId,
    fieldErrors,
    message,
  );
}
