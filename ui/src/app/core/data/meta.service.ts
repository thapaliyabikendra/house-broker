import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { RuntimeConfigService } from '../config/runtime-config.service';

interface MetaResponse {
  currency?: string;
  currencyCode?: string;
}

@Injectable({ providedIn: 'root' })
export class MetaService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(RuntimeConfigService);

  readonly currencyCode = signal('NPR');

  async load(): Promise<void> {
    try {
      const response = await firstValueFrom(
        this.http.get<MetaResponse>(`${this.config.value().apiBaseUrl}/v1/meta`),
      );
      const code = response.currencyCode ?? response.currency;
      if (typeof code === 'string' && code.length > 0) {
        this.currencyCode.set(code);
      }
    } catch {
      // keep default currency
    }
  }
}
