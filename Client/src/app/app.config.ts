import { ApplicationConfig } from '@angular/core';
import { TitleStrategy, provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideIcons } from '@ng-icons/core';
import { ICONES } from './core/icones';
import { routes } from './app.routes';
import { TituloDaPaginaStrategy } from './core/titulo-pagina.strategy';
import { AuthInterceptor } from './auth/auth.interceptor';
import { HttpErrorInterceptor } from './auth/http.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    { provide: TitleStrategy, useClass: TituloDaPaginaStrategy },
    provideHttpClient(
      withInterceptors([
        AuthInterceptor,
        HttpErrorInterceptor
      ])
    ),
    provideIcons(ICONES),
  ]
};
