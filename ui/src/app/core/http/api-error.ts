/** One field-level error from the API: the stable code and the message localized for the request culture. */
export interface FieldErrorDetail {
  code: string;
  message: string;
}

export interface ProblemDetails {
  type?: string | null;
  title?: string | null;
  status?: number | string | null;
  detail?: string | null;
  instance?: string | null;
  code?: string;
  correlationId?: string;
  field?: string;
  /** Per-field errors, keyed by camelCase input field name. */
  errors?: Record<string, FieldErrorDetail[]>;
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string | undefined,
    public readonly correlationId: string | undefined,
    public readonly fieldErrors: Record<string, FieldErrorDetail[]> | undefined,
    message?: string,
  ) {
    super(message ?? code ?? 'Unexpected error');
    this.name = 'ApiError';
  }
}
