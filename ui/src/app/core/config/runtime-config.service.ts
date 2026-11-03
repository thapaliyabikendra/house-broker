import { Injectable, signal } from '@angular/core';

export interface RuntimeConfig {
  apiBaseUrl: string;
  ssoIssuer: string;
}

@Injectable({ providedIn: 'root' })
export class RuntimeConfigService {
  private readonly config = signal<RuntimeConfig>({ apiBaseUrl: '/api', ssoIssuer: '/sso' });
  readonly value = this.config.asReadonly();
  async load(): Promise<void> {
    try {
      const response = await fetch('/assets/config.json');
      if (response.ok) {
        const next = (await response.json()) as RuntimeConfig;
        this.config.set(next);
      }
    } catch {
      // keep defaults
    }
  }
}
