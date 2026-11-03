import {
  ApplicationConfig,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  inject,
} from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import { routes } from './app.routes';
import { RuntimeConfigService } from './core/config/runtime-config.service';
import { AppTranslocoLoader } from './core/i18n/app-transloco.loader';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(),
    provideRouter(routes),
    provideAppInitializer(() => inject(RuntimeConfigService).load()),
    provideTransloco({
      config: {
        availableLangs: ['en', 'ne'],
        defaultLang: 'en',
        fallbackLang: 'en',
        reRenderOnLangChange: true,
        prodMode: false,
      },
      loader: AppTranslocoLoader,
    }),
  ],
};
