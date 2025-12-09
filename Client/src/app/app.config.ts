import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { routes } from './app.routes';
import { AuthInterceptor } from './auth/auth.interceptor';
import { DebugInterceptor } from './auth/http.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([
        DebugInterceptor, // Adicione para debug
        AuthInterceptor
      ])
    ),
    provideAnimations(),
  ]
};
