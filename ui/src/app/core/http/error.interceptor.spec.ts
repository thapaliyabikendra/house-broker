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
});
