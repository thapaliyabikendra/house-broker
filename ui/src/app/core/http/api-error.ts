import type { ProblemDetails as GeneratedProblemDetails } from '../../api/models';

/** One field-level error from the API: the stable code and the message localized for the request culture. */
export interface FieldErrorDetail {
  code: string;
  message: string;
}

/**
 * Wire shape of an RFC 7807 error body: the generated standard fields plus our extensions.
 * Extension values are untrusted, so they are typed `unknown` and normalised by the interceptor.
 */
export interface ProblemDetails extends GeneratedProblemDetails {
  code?: unknown;
  correlationId?: unknown;
  field?: unknown;
  relatedIds?: unknown;
  /** Our `{ code, message }` entries, or the default ASP.NET string arrays. */
  errors?: unknown;
}

export interface ApiErrorInit {
  status: number;
  code?: string;
  correlationId?: string;
  /** Camel-case name of the first field that failed, when the failure is field-specific. */
  field?: string;
  /** Per-field errors, keyed by camel-case input field name. */
  fieldErrors?: Record<string, FieldErrorDetail[]>;
  /** Ids of existing records that must be fixed before the request can succeed. */
  relatedIds?: readonly string[];
  message?: string;
}

export class ApiError extends Error {
  readonly status: number;
  readonly code: string | undefined;
  readonly correlationId: string | undefined;
  readonly field: string | undefined;
  readonly fieldErrors: Record<string, FieldErrorDetail[]> | undefined;
  readonly relatedIds: readonly string[];

  constructor(init: ApiErrorInit) {
    super(init.message ?? init.code ?? 'Unexpected error');
    this.name = 'ApiError';
    this.status = init.status;
    this.code = init.code;
    this.correlationId = init.correlationId;
    this.field = init.field;
    this.fieldErrors = init.fieldErrors;
    this.relatedIds = init.relatedIds ?? [];
  }
}
