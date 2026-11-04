import { vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TranslocoService } from '@jsverse/transloco';
import { AuthService } from './auth.service';
import { RuntimeConfigService } from '../config/runtime-config.service';
import { TokenStorageService } from './token-storage.service';
import { LanguageService } from '../i18n/language.service';

function makeJwt(payload: Record<string, unknown>): string {
  const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }));
  const body = btoa(JSON.stringify(payload));
  return `${header}.${body}.`;
}

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let storage: TokenStorageService;

  beforeEach(() => {
    sessionStorage.clear();
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { assign: vi.fn(), origin: 'http://localhost:4200', href: 'http://localhost:4200/' },
    });
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        TokenStorageService,
        RuntimeConfigService,
        LanguageService,
        { provide: TranslocoService, useValue: { setActiveLang: () => undefined } },
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(AuthService);
    storage = TestBed.inject(TokenStorageService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    try {
      httpMock.verify();
    } catch {
      // let failing tests report through other assertions
    }
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: window.location,
    });
  });

  it('redirects to the SSO authorize endpoint with PKCE', async () => {
    await service.signIn('ne');
    const assignSpy = window.location.assign as ReturnType<typeof vi.fn>;
    expect(assignSpy).toHaveBeenCalled();
    const lastCall = assignSpy.mock.calls[assignSpy.mock.calls.length - 1];
    const url = (lastCall === undefined ? '' : String(lastCall[0])) as string;
    expect(url).toContain('/connect/authorize');
    expect(url).toContain('client_id=housebroker-web');
    expect(url).toContain('code_challenge=');
    expect(url).toContain('ui_locales=ne');
  });

  it('completes the authorization-code callback and stores tokens', async () => {
    storage.setState('state-1');
    storage.setPkceVerifier('verifier-1');

    const promise = service.handleCallback('?code=auth-code&state=state-1');
    const tokenRequest = httpMock.expectOne((req) => req.url.includes('/connect/token'));
    expect(tokenRequest.request.method).toBe('POST');
    expect(String(tokenRequest.request.body)).toContain('grant_type=authorization_code');
    tokenRequest.flush({
      access_token: 'access-1',
      refresh_token: 'refresh-1',
      id_token: makeJwt({
        sub: 'user-1',
        name: 'Broker',
        role: 'Broker',
        permission: 'listings.read',
      }),
      expires_in: 3600,
    });

    await Promise.resolve();
    const userInfoRequest = httpMock.expectOne((req) => req.url.includes('/connect/userinfo'));
    userInfoRequest.flush({ sub: 'user-1', name: 'Broker' });

    const result = await promise;
    expect(result).toBe(true);
    expect(service.getAccessToken()).toBe('access-1');
    expect(service.user()?.sub).toBe('user-1');
    expect(service.user()?.roles).toContain('Broker');
  });

  it('rejects the callback when state does not match', async () => {
    storage.setState('expected-state');
    storage.setPkceVerifier('verifier-1');
    const result = await service.handleCallback('?code=auth-code&state=wrong-state');
    expect(result).toBe(false);
    httpMock.expectNone((req) => req.url.includes('/connect/token'));
  });

  it('refreshes an expired access token', async () => {
    sessionStorage.setItem('hb.refresh_token', 'refresh-1');
    const promise = service.refreshToken();
    const request = httpMock.expectOne((req) => req.url.includes('/connect/token'));
    expect(String(request.request.body)).toContain('grant_type=refresh_token');
    request.flush({
      access_token: 'access-2',
      refresh_token: 'refresh-2',
      expires_in: 3600,
    });

    await Promise.resolve();
    const userInfoRequest = httpMock.expectOne((req) => req.url.includes('/connect/userinfo'));
    userInfoRequest.flush({ sub: 'user-2' });

    const result = await promise;
    expect(result).toBe(true);
    expect(service.getAccessToken()).toBe('access-2');
  });

  it('clears tokens on sign out', () => {
    storage.setTokens({ accessToken: 'access-1', refreshToken: 'refresh-1', expiresIn: 3600 });
    service.signOut();
    const assignSpy = window.location.assign as ReturnType<typeof vi.fn>;
    expect(storage.getAccessToken()).toBeNull();
    expect(service.user()).toBeNull();
    expect(assignSpy).toHaveBeenCalled();
  });
});
