import { Injectable, signal } from '@angular/core';

export interface RuntimeConfig {
  apiBaseUrl: string;
  ssoIssuer: string;
  clientId: string;
  scopes: string[];
}

@Injectable({ providedIn: 'root' })
export class RuntimeConfigService {
  private readonly config = signal<RuntimeConfig>({
    apiBaseUrl: '/api',
    ssoIssuer: '/sso',
    clientId: 'housebroker-web',
    scopes: [
      'openid',
      'profile',
      'email',
      'phone',
      'offline_access',
      'listings.read',
      'contacts.read',
      'listings.write',
      'commission.read',
      'admin.commission',
    ],
  });

  readonly value = this.config.asReadonly();

  async load(): Promise<void> {
    try {
      const response = await fetch('/assets/config.json');
      if (response.ok) {
        const next = (await response.json()) as Partial<RuntimeConfig>;
        this.config.update((current) => ({
          apiBaseUrl:
            typeof next.apiBaseUrl === 'string' && next.apiBaseUrl.length > 0
              ? next.apiBaseUrl
              : current.apiBaseUrl,
          ssoIssuer:
            typeof next.ssoIssuer === 'string' && next.ssoIssuer.length > 0
              ? next.ssoIssuer
              : current.ssoIssuer,
          clientId:
            typeof next.clientId === 'string' && next.clientId.length > 0
              ? next.clientId
              : current.clientId,
          scopes:
            Array.isArray(next.scopes) && next.scopes.every((item) => typeof item === 'string')
              ? next.scopes
              : current.scopes,
        }));
      }
    } catch {
      // keep defaults
    }
  }
}
