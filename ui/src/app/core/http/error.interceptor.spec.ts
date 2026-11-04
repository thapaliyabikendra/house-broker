import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { ApiError } from './api-error';
import { errorInterceptor } from './error.interceptor';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            getRefreshToken: () => null,
            signOut: () => undefined,
          },
        },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('maps an HTTP error to ApiError with code and correlation id', async () => {
    const responsePromise = firstValueFrom(http.get('/api/v1/meta')).catch(
      (error: unknown) => error,
    );

    const request = httpMock.expectOne('/api/v1/meta');
    request.flush(
      { code: 'listing.not_found', title: 'Listing not found' },
      {
        status: 404,
        statusText: 'Not Found',
        headers: { 'X-Correlation-Id': 'corr-1' },
      },
    );

    const error = (await responsePromise) as ApiError;
    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(404);
    expect(error.code).toBe('listing.not_found');
    expect(error.correlationId).toBe('corr-1');
  });

  async function failWith(body: Record<string, unknown> | string, status = 400): Promise<ApiError> {
    const responsePromise = firstValueFrom(http.get('/api/v1/admin/commission-rates')).catch(
      (error: unknown) => error,
    );
    httpMock.expectOne('/api/v1/admin/commission-rates').flush(body, {
      status,
      statusText: 'Error',
    });
    return (await responsePromise) as ApiError;
  }

  it('carries the field and relatedIds from the problem details', async () => {
    const error = await failWith(
      {
        code: 'conflict',
        title: 'Conflicts with an existing tier.',
        status: 409,
        field: 'MinPrice',
        relatedIds: ['0190a1', '0190a2'],
        errors: { minPrice: [{ code: 'overlap', message: 'Overlaps another tier.' }] },
      },
      409,
    );

    expect(error.field).toBe('minPrice');
    expect(error.relatedIds).toEqual(['0190a1', '0190a2']);
    expect(error.fieldErrors).toEqual({
      minPrice: [{ code: 'overlap', message: 'Overlaps another tier.' }],
    });
    expect(error.message).toBe('Conflicts with an existing tier.');
  });

  it('defaults field and relatedIds when the problem details do not carry them', async () => {
    const error = await failWith({ code: 'listing.not_found', title: 'Not found' }, 404);

    expect(error.field).toBeUndefined();
    expect(error.relatedIds).toEqual([]);
    expect(error.fieldErrors).toBeUndefined();
  });

  it('ignores relatedIds that are not an array of strings', async () => {
    const error = await failWith({ title: 'Bad', relatedIds: [1, 'ok', null] });

    expect(error.relatedIds).toEqual(['ok']);
  });

  it('normalises default ASP.NET ValidationProblemDetails string errors', async () => {
    const error = await failWith({
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: {
        MinPrice: ['The MinPrice field is required.'],
        '$.ratePercent': ['The field must be between 0 and 100.', 'Must be a number.'],
      },
    });

    expect(error.fieldErrors).toEqual({
      minPrice: [{ code: 'validation.failed', message: 'The MinPrice field is required.' }],
      ratePercent: [
        { code: 'validation.failed', message: 'The field must be between 0 and 100.' },
        { code: 'validation.failed', message: 'Must be a number.' },
      ],
    });
  });

  it('maps PascalCase ASP.NET fields to camelCase and keeps our {code,message} shape', async () => {
    const error = await failWith({
      title: 'Invalid',
      errors: {
        EffectiveFrom: [{ message: 'Effective date is required.' }],
        maxPrice: [{ code: 'range', message: 'Too low.' }],
      },
    });

    expect(error.fieldErrors).toEqual({
      effectiveFrom: [{ code: 'validation.failed', message: 'Effective date is required.' }],
      maxPrice: [{ code: 'range', message: 'Too low.' }],
    });
  });

  it('ignores error bodies that are not JSON objects', async () => {
    const error = await failWith('<html>Bad gateway</html>', 502);

    expect(error.fieldErrors).toBeUndefined();
    expect(error.relatedIds).toEqual([]);
    expect(error.field).toBeUndefined();
  });

  it('maps 422 to the idempotency conflict message, not a prompt to correct fields', async () => {
    const error = await failWith({ title: undefined }, 422);

    expect(error.message).toBe(
      'This submission conflicts with an earlier request. Reload the page and try again.',
    );
    expect(error.message).not.toContain('highlighted fields');
  });

  it('uses the server title as the message when the problem has one, even for 422', async () => {
    const error = await failWith({ title: 'Localized conflict text.' }, 422);

    expect(error.message).toBe('Localized conflict text.');
  });
});
