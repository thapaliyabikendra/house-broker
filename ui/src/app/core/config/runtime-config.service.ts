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
        this.config.update((current) => ({ ...current, ...next }));
      }
    } catch {
      // keep defaults
    }
  }
}
