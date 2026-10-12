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
      // A ordem importa: o HttpErrorInterceptor fica por FORA para só avisar o erro FINAL; assim um 401
      // que o AuthInterceptor consegue renovar e repetir não abre um toast "Não autorizado" à toa.
      withInterceptors([
        HttpErrorInterceptor,
        AuthInterceptor
      ])
    ),
    provideIcons(ICONES),
  ]
};
