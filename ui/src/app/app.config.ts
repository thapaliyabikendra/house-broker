import type { ApplicationConfig } from '@angular/core';
import { provideAppInitializer, provideBrowserGlobalErrorListeners, inject } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { Api } from './api/api';
import { provideApiConfiguration } from './api/api-configuration';
import { provideTransloco } from '@jsverse/transloco';
import { routes } from './app.routes';
import { RuntimeConfigService } from './core/config/runtime-config.service';
import { AuthService } from './core/auth/auth.service';
import { MetaService } from './core/data/meta.service';
import { acceptLanguageInterceptor } from './core/http/accept-language.interceptor';
import { authTokenInterceptor } from './core/http/auth-token.interceptor';
import { correlationIdInterceptor } from './core/http/correlation-id.interceptor';
import { errorInterceptor } from './core/http/error.interceptor';
import { AppTranslocoLoader } from './core/i18n/app-transloco.loader';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(
      withInterceptors([
        correlationIdInterceptor,
        acceptLanguageInterceptor,
        authTokenInterceptor,
        errorInterceptor,
      ]),
    ),
    provideRouter(routes),
    provideApiConfiguration('/api'),
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
    provideAppInitializer(() => {
      const runtimeConfig = inject(RuntimeConfigService);
      const api = inject(Api);
      const auth = inject(AuthService);
      const meta = inject(MetaService);
      return runtimeConfig.load().then(() => {
        api.rootUrl = runtimeConfig.value().apiBaseUrl;
        void meta.load();
        return auth.initialize();
      });
    }),
  ],
};
