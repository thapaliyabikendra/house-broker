import type { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { LanguageService } from '../i18n/language.service';

export const acceptLanguageInterceptor: HttpInterceptorFn = (req, next) => {
  const lang = inject(LanguageService).currentLang();
  if (req.headers.has('Accept-Language')) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { 'Accept-Language': lang } }));
};
