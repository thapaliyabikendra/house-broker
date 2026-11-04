import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { correlationIdInterceptor } from './correlation-id.interceptor';

describe('correlationIdInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([correlationIdInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('adds a correlation id header when one is absent', () => {
    http.get('/api/v1/meta').subscribe();
    const request = httpMock.expectOne('/api/v1/meta');
    expect(request.request.headers.get('X-Correlation-Id')).toBeTruthy();
    request.flush({});
  });

  it('preserves an existing correlation id header', () => {
    http.get('/api/v1/meta', { headers: { 'X-Correlation-Id': 'client-id-1' } }).subscribe();
    const request = httpMock.expectOne('/api/v1/meta');
    expect(request.request.headers.get('X-Correlation-Id')).toBe('client-id-1');
    request.flush({});
  });
});
