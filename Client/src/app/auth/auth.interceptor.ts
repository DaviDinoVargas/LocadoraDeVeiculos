import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, throwError } from 'rxjs';
import { catchError, finalize, shareReplay, switchMap } from 'rxjs/operators';
import { AccessToken, AuthService } from './auth.service';

// Renovação de sessão compartilhada: várias requisições que tomam 401 ao mesmo tempo usam a MESMA
// chamada a /auth/rotacionar. O refresh token é rotativo; duas chamadas com o mesmo cookie fariam a
// segunda falhar e derrubar a sessão.
let renovacao$: Observable<AccessToken> | null = null;

function renovarSessao(auth: AuthService): Observable<AccessToken> {
  if (!renovacao$) {
    renovacao$ = auth.rotacionarToken().pipe(
      finalize(() => (renovacao$ = null)),
      shareReplay({ bufferSize: 1, refCount: false })
    );
  }
  return renovacao$;
}

export const AuthInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const accessToken = auth.getAccessToken();
  const temSessao = !!accessToken;
  const ehRotaDeAuth = req.url.includes('/auth/');

  const comToken = accessToken && !auth.isTokenExpirado()
    ? req.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
    : req;

  return next(comToken).pipe(
    catchError((error: HttpErrorResponse) => {
      // 401 numa chamada comum de quem tinha sessão: o access token venceu OU foi revogado no servidor
      // (outra aba/login rotacionou o refresh token e invalidou o anterior). Antes só o caso "venceu
      // pelo relógio" renovava e o "revogado" virava um 401 solto na tela. Renova uma vez e repete.
      if (error.status === 401 && temSessao && !ehRotaDeAuth) {
        return renovarSessao(auth).pipe(
          // Só a falha da RENOVAÇÃO (refresh inválido/expirado) encerra a sessão e volta ao login...
          catchError(falha => {
            auth.limparStorage();
            void router.navigate(['/login']);
            return throwError(() => falha);
          }),
          // ...e um erro da requisição repetida (400, 409...) segue para a tela normalmente.
          switchMap(token => next(req.clone({ setHeaders: { Authorization: `Bearer ${token.accessToken}` } })))
        );
      }

      return throwError(() => error);
    })
  );
};
