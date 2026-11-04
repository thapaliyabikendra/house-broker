import { inject, Injectable } from '@angular/core';
import { ClockService } from '../time/clock.service';

const ACCESS_TOKEN_KEY = 'hb.access_token';
const REFRESH_TOKEN_KEY = 'hb.refresh_token';
const ID_TOKEN_KEY = 'hb.id_token';
const EXPIRES_AT_KEY = 'hb.expires_at';
const PKCE_VERIFIER_KEY = 'hb.pkce_verifier';
const STATE_KEY = 'hb.state';

@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  private readonly clock = inject(ClockService);

  getAccessToken(): string | null {
    return sessionStorage.getItem(ACCESS_TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return sessionStorage.getItem(REFRESH_TOKEN_KEY);
  }

  getIdToken(): string | null {
    return sessionStorage.getItem(ID_TOKEN_KEY);
  }

  getExpiresAt(): number | null {
    const raw = sessionStorage.getItem(EXPIRES_AT_KEY);
    return raw === null ? null : Number(raw);
  }

  setTokens(tokens: {
    accessToken: string;
    refreshToken?: string | null;
    idToken?: string | null;
    expiresIn?: number;
  }): void {
    sessionStorage.setItem(ACCESS_TOKEN_KEY, tokens.accessToken);
    if (tokens.refreshToken) {
      sessionStorage.setItem(REFRESH_TOKEN_KEY, tokens.refreshToken);
    }
    if (tokens.idToken) {
      sessionStorage.setItem(ID_TOKEN_KEY, tokens.idToken);
    }
    if (tokens.expiresIn !== undefined) {
      sessionStorage.setItem(
        EXPIRES_AT_KEY,
        String(this.clock.now().getTime() + tokens.expiresIn * 1000),
      );
    }
  }

  clearTokens(): void {
    sessionStorage.removeItem(ACCESS_TOKEN_KEY);
    sessionStorage.removeItem(REFRESH_TOKEN_KEY);
    sessionStorage.removeItem(ID_TOKEN_KEY);
    sessionStorage.removeItem(EXPIRES_AT_KEY);
  }

  getPkceVerifier(): string | null {
    return sessionStorage.getItem(PKCE_VERIFIER_KEY);
  }

  setPkceVerifier(value: string): void {
    sessionStorage.setItem(PKCE_VERIFIER_KEY, value);
  }

  clearPkceVerifier(): void {
    sessionStorage.removeItem(PKCE_VERIFIER_KEY);
  }

  getState(): string | null {
    return sessionStorage.getItem(STATE_KEY);
  }

  setState(value: string): void {
    sessionStorage.setItem(STATE_KEY, value);
  }

  clearState(): void {
    sessionStorage.removeItem(STATE_KEY);
  }
}
