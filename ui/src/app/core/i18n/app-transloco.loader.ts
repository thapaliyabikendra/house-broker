import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { TranslocoLoader } from '@jsverse/transloco';

@Injectable({ providedIn: 'root' })
export class AppTranslocoLoader implements TranslocoLoader {
  constructor(private readonly http: HttpClient) {}
  getTranslation(lang: string) {
    return this.http.get(`/assets/i18n/${lang}.json`);
  }
}
