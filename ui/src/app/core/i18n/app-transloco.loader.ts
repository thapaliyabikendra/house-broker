import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { TranslocoLoader } from '@jsverse/transloco';

@Injectable({ providedIn: 'root' })
export class AppTranslocoLoader implements TranslocoLoader {
  private readonly http = inject(HttpClient);

  getTranslation(lang: string) {
    return this.http.get(`/assets/i18n/${lang}.json`);
  }
}
