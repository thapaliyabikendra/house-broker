export interface ProblemDetails {
  type?: string | null;
  title?: string | null;
  status?: number | string | null;
  detail?: string | null;
  instance?: string | null;
  code?: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string | undefined,
    public readonly correlationId: string | undefined,
    public readonly fieldErrors: Record<string, string[]> | undefined,
    message?: string,
  ) {
    super(message ?? code ?? 'Unexpected error');
    this.name = 'ApiError';
  }
}
