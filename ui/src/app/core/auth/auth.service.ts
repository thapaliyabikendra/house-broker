import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { RuntimeConfigService } from '../config/runtime-config.service';
import { TokenStorageService } from './token-storage.service';
import type { AuthUser } from './auth-user.model';

interface TokenResponse {
  access_token: string;
  refresh_token?: string;
  id_token?: string;
  expires_in?: number;
  token_type?: string;
  scope?: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(RuntimeConfigService);
  private readonly storage = inject(TokenStorageService);

  private readonly userSignal = signal<AuthUser | null>(null);
  readonly user = this.userSignal.asReadonly();
  readonly isAuthenticated = computed(
    () => this.userSignal() !== null && this.storage.getAccessToken() !== null,
  );

  constructor() {
    this.restore();
  }

  private restore(): void {
    const token = this.storage.getAccessToken();
    if (token) {
      const claims = this.decodeJwt(this.storage.getIdToken() ?? token);
      this.userSignal.set(this.buildUser(claims, token));
    }
  }

  async initialize(): Promise<void> {
    const access = this.storage.getAccessToken();
    const refresh = this.storage.getRefreshToken();
    if (access && !this.isTokenExpired()) {
      this.restore();
      return;
    }
    if (refresh) {
      try {
        await this.refreshToken();
      } catch {
        this.signOut(false);
      }
    }
  }

  async signIn(uiLocales?: string): Promise<void> {
    const verifier = this.generateRandomString(64);
    const challenge = await this.createCodeChallenge(verifier);
    const state = this.generateRandomString(32);
    this.storage.setPkceVerifier(verifier);
    this.storage.setState(state);

    const cfg = this.config.value();
    const redirectUri = `${window.location.origin}/auth/callback`;
    const params = new URLSearchParams({
      client_id: cfg.clientId,
      redirect_uri: redirectUri,
      response_type: 'code',
      scope: cfg.scopes.join(' '),
      state,
      code_challenge: challenge,
      code_challenge_method: 'S256',
    });
    if (uiLocales) {
      params.set('ui_locales', uiLocales);
    }

    window.location.assign(
      `${this.absolute(cfg.ssoIssuer)}/connect/authorize?${params.toString()}`,
    );
  }

  async handleCallback(search: string): Promise<boolean> {
    const params = new URLSearchParams(search);
    const error = params.get('error');
    if (error) {
      return false;
    }
    const code = params.get('code');
    const state = params.get('state');
    if (!code || state === null || state !== this.storage.getState()) {
      return false;
    }
    this.storage.clearState();
    const verifier = this.storage.getPkceVerifier();
    if (!verifier) {
      return false;
    }
    this.storage.clearPkceVerifier();

    const cfg = this.config.value();
    const redirectUri = `${window.location.origin}/auth/callback`;
    const body = new URLSearchParams({
      grant_type: 'authorization_code',
      client_id: cfg.clientId,
      code,
      redirect_uri: redirectUri,
      code_verifier: verifier,
    });

    try {
      const response = await firstValueFrom(
        this.http.post<TokenResponse>(
          `${this.absolute(cfg.ssoIssuer)}/connect/token`,
          body.toString(),
          {
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            withCredentials: true,
          },
        ),
      );
      this.storage.setTokens({
        accessToken: response.access_token,
        refreshToken: response.refresh_token,
        idToken: response.id_token,
        expiresIn: response.expires_in,
      });
      await this.loadUser();
      return true;
    } catch {
      return false;
    }
  }

  async refreshToken(): Promise<boolean> {
    const refresh = this.storage.getRefreshToken();
    if (!refresh) {
      return false;
    }
    const cfg = this.config.value();
    const body = new URLSearchParams({
      grant_type: 'refresh_token',
      client_id: cfg.clientId,
      refresh_token: refresh,
    });

    try {
      const response = await firstValueFrom(
        this.http.post<TokenResponse>(
          `${this.absolute(cfg.ssoIssuer)}/connect/token`,
          body.toString(),
          {
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            withCredentials: true,
          },
        ),
      );
      this.storage.setTokens({
        accessToken: response.access_token,
        refreshToken: response.refresh_token ?? refresh,
        idToken: response.id_token,
        expiresIn: response.expires_in,
      });
      await this.loadUser();
      return true;
    } catch {
      return false;
    }
  }

  signOut(redirect = true): void {
    this.storage.clearTokens();
    this.userSignal.set(null);
    if (!redirect) {
      return;
    }
    const cfg = this.config.value();
    const idToken = this.storage.getIdToken();
    const params = new URLSearchParams();
    params.set('post_logout_redirect_uri', window.location.origin);
    if (idToken) {
      params.set('id_token_hint', idToken);
    }
    window.location.assign(`${this.absolute(cfg.ssoIssuer)}/connect/logout?${params.toString()}`);
  }

  getAccessToken(): string | null {
    return this.storage.getAccessToken();
  }

  getRefreshToken(): string | null {
    return this.storage.getRefreshToken();
  }

  isTokenExpired(): boolean {
    const expiresAt = this.storage.getExpiresAt();
    if (expiresAt === null) {
      return false;
    }
    return Date.now() >= expiresAt - 30_000;
  }

  private async loadUser(): Promise<void> {
    const accessToken = this.storage.getAccessToken();
    if (!accessToken) {
      this.userSignal.set(null);
      return;
    }
    const idToken = this.storage.getIdToken();
    const claims: Record<string, unknown> = idToken
      ? this.decodeJwt(idToken)
      : this.decodeJwt(accessToken);

    try {
      const info = await firstValueFrom(
        this.http.get<Record<string, unknown>>(
          `${this.absolute(this.config.value().ssoIssuer)}/connect/userinfo`,
          {
            headers: { Authorization: `Bearer ${accessToken}` },
          },
        ),
      );
      Object.assign(claims, info ?? {});
    } catch {
      // fall back to token claims
    }

    this.userSignal.set(this.buildUser(claims, accessToken));
  }

  private buildUser(claims: Record<string, unknown>, accessToken: string): AuthUser {
    const scopeClaim = claims['scope'] ?? claims['scp'];
    const roles = this.toStringArray(claims['role'] ?? claims['roles']);
    const permissions = this.toStringArray(claims['permission'] ?? claims['permissions']);
    const scopes =
      typeof scopeClaim === 'string'
        ? scopeClaim.split(' ').filter(Boolean)
        : this.toStringArray(scopeClaim);

    // If no permissions claim present (should not happen), we leave empty.
    if (roles.length === 0 && accessToken) {
      // try to parse role from token again
    }

    return {
      sub: String(claims['sub'] ?? ''),
      name: this.asOptionalString(claims['name'] ?? claims['preferred_username']),
      email: this.asOptionalString(claims['email']),
      phoneNumber: this.asOptionalString(claims['phone_number']),
      roles,
      permissions,
      scopes,
    };
  }

  hasPermission(permission: string): boolean {
    const user = this.userSignal();
    return !!user && user.permissions.includes(permission);
  }

  hasScope(scope: string): boolean {
    const user = this.userSignal();
    return !!user && user.scopes.includes(scope);
  }

  hasRole(role: string): boolean {
    const user = this.userSignal();
    return !!user && user.roles.includes(role);
  }

  private toStringArray(value: unknown): string[] {
    if (Array.isArray(value)) {
      return value.map((item) => String(item));
    }
    if (typeof value === 'string') {
      return value.length > 0 ? [value] : [];
    }
    return [];
  }

  private asOptionalString(value: unknown): string | undefined {
    return typeof value === 'string' ? value : undefined;
  }

  private decodeJwt(token: string): Record<string, unknown> {
    try {
      const payload = token.split('.')[1];
      if (!payload) {
        return {};
      }
      const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
      const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=');
      const json = decodeURIComponent(
        atob(padded)
          .split('')
          .map((c) => `%${c.charCodeAt(0).toString(16).padStart(2, '0')}`)
          .join(''),
      );
      return JSON.parse(json) as Record<string, unknown>;
    } catch {
      return {};
    }
  }

  private generateRandomString(length: number): string {
    const array = new Uint8Array(length);
    crypto.getRandomValues(array);
    return Array.from(array, (byte) => byte.toString(16).padStart(2, '0'))
      .join('')
      .slice(0, length);
  }

  private async createCodeChallenge(verifier: string): Promise<string> {
    const data = new TextEncoder().encode(verifier);
    const digest = await crypto.subtle.digest('SHA-256', data);
    return this.base64UrlEncode(digest);
  }

  private base64UrlEncode(input: ArrayBuffer): string {
    const bytes = new Uint8Array(input);
    let binary = '';
    for (const byte of bytes) {
      binary += String.fromCharCode(byte);
    }
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  }

  private absolute(url: string): string {
    return url.startsWith('http') ? url : `${window.location.origin}${url}`;
  }
}
