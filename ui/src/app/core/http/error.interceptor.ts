import type { HttpInterceptorFn } from '@angular/common/http';
import { HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import type { FieldErrorDetail, ProblemDetails } from './api-error';
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

/** Code used for ASP.NET string errors, which carry no stable code of their own. */
const VALIDATION_FAILED = 'validation.failed';

function toApiError(error: HttpErrorResponse, transloco?: TranslocoService | null): ApiError {
  const problem = asProblem(error.error);
  const correlationId =
    error.headers.get('X-Correlation-Id') ?? nonEmptyString(problem?.correlationId);
  const fieldErrors = parseFieldErrors(problem?.errors);
  const field = nonEmptyString(problem?.field);

  let message = nonEmptyString(problem?.title) ?? nonEmptyString(problem?.detail);
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
      // The API uses 422 only when an Idempotency-Key is reused with a different body; field errors are 400.
      message = translate(
        'errors.idempotencyConflict',
        'This submission conflicts with an earlier request. Reload the page and try again.',
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

  return new ApiError({
    status: error.status,
    code: nonEmptyString(problem?.code),
    correlationId,
    field: field === undefined ? undefined : normalizeFieldName(field),
    fieldErrors,
    relatedIds: parseRelatedIds(problem?.relatedIds),
    message,
  });
}

function asProblem(body: unknown): ProblemDetails | null {
  return typeof body === 'object' && body !== null && !Array.isArray(body)
    ? (body as ProblemDetails)
    : null;
}

function nonEmptyString(value: unknown): string | undefined {
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

function parseRelatedIds(value: unknown): string[] {
  return Array.isArray(value)
    ? value.filter((id): id is string => typeof id === 'string' && id.length > 0)
    : [];
}

/**
 * Converts a server field name to the camel-case name used by the forms. Default ASP.NET keys
 * are PascalCase property names, sometimes prefixed with `$.` for JSON binding errors.
 */
export function normalizeFieldName(name: string): string {
  const trimmed = name.trim();
  const withoutPath = trimmed.startsWith('$.') ? trimmed.slice(2) : trimmed;
  return withoutPath.charAt(0).toLowerCase() + withoutPath.slice(1);
}

/**
 * Accepts both shapes the API can send: our `{ code, message }` entries and the default
 * ASP.NET `string[]`. Entries with no message are dropped; keys that normalise to the same
 * field are merged.
 */
function parseFieldErrors(value: unknown): Record<string, FieldErrorDetail[]> | undefined {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return undefined;
  }
  const byField = new Map<string, FieldErrorDetail[]>();
  for (const [rawField, rawEntries] of Object.entries(value)) {
    const entries = toFieldErrorDetails(rawEntries);
    if (entries.length === 0) {
      continue;
    }
    const field = normalizeFieldName(rawField);
    byField.set(field, [...(byField.get(field) ?? []), ...entries]);
  }
  return byField.size > 0 ? Object.fromEntries(byField) : undefined;
}

function toFieldErrorDetails(value: unknown): FieldErrorDetail[] {
  const items: unknown[] = Array.isArray(value) ? value : [value];
  return items.flatMap((item): FieldErrorDetail[] => {
    if (typeof item === 'string') {
      return item.length > 0 ? [{ code: VALIDATION_FAILED, message: item }] : [];
    }
    if (typeof item === 'object' && item !== null) {
      const entry = item as { code?: unknown; message?: unknown };
      const message = nonEmptyString(entry.message);
      if (message) {
        return [{ code: nonEmptyString(entry.code) ?? VALIDATION_FAILED, message }];
      }
    }
    return [];
  });
}
