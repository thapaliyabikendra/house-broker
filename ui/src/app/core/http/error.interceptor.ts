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

  let message = problem?.title ?? problem?.detail ?? undefined;
  if (!message) {
    if (error.status === 0) {
      message = 'Network error. Please check your connection and try again.';
    } else if (error.status === 401) {
      message = 'Your session expired. Please sign in again.';
    } else if (error.status === 403) {
      message = 'You are not allowed to perform this action.';
    } else if (error.status === 404) {
      message = 'The requested resource was not found.';
    } else if (error.status === 409) {
      message = 'The request conflicts with the current state. Please refresh and try again.';
    } else if (error.status === 412) {
      message = 'This record was changed by someone else. Please refresh and try again.';
    } else if (error.status === 422) {
      message = 'Please correct the highlighted fields and try again.';
    } else if (error.status === 428) {
      message = 'A version (If-Match) is required for this change.';
    } else if (error.status >= 500) {
      message = 'Something went wrong on the server. Please try again later.';
    } else {
      message = error.message || 'Unexpected error.';
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
