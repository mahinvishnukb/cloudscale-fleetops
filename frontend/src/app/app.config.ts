import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';

import { authInterceptor } from './core/auth.interceptor';
import { routes } from './app.routes';

/**
 * Angular 22 runs zoneless by default — there is no zone.js in the dependency tree.
 * Change detection is driven entirely by signals, which is why component state here
 * is held in signals rather than plain fields.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      withComponentInputBinding(),
      withInMemoryScrolling({ scrollPositionRestoration: 'top' }),
    ),
    provideHttpClient(withInterceptors([authInterceptor])),
  ],
};
