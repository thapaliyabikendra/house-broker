import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../auth/auth.service';
import { RuntimeConfigService } from '../config/runtime-config.service';
import { authTokenInterceptor } from './auth-token.interceptor';

describe('authTokenInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authTokenInterceptor])),
        provideHttpClientTesting(),
        {
          provide: AuthService,
          useValue: {
            getAccessToken: () => 'secret-token',
            isTokenExpired: () => false,
          },
        },
        {
          provide: RuntimeConfigService,
          useValue: { value: () => ({ apiBaseUrl: '/api' }) },
        },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('attaches the bearer token to API requests', () => {
    http.get('/api/v1/meta').subscribe();
    const request = httpMock.expectOne('/api/v1/meta');
    expect(request.request.headers.get('Authorization')).toBe('Bearer secret-token');
    request.flush({});
  });

  it('does not attach a bearer token to non-API requests', () => {
    http.get('/assets/config.json').subscribe();
    const request = httpMock.expectOne('/assets/config.json');
    expect(request.request.headers.get('Authorization')).toBeNull();
    request.flush({});
  });
});
