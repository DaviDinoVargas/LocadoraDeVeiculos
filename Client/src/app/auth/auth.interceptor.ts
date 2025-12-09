import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';
import { Router } from '@angular/router';
import { Observable, BehaviorSubject, throwError, from } from 'rxjs';
import { catchError, filter, switchMap, take, mergeMap } from 'rxjs/operators';

let isRefreshing = false;
const refreshTokenSubject = new BehaviorSubject<string | null>(null);

export const AuthInterceptor: HttpInterceptorFn = (req, next) => {

  const auth = inject(AuthService);
  const router = inject(Router);

  const accessToken = auth.getAccessToken();

  let authReq = req;

  if (accessToken && !auth.isTokenExpirado()) {
    authReq = req.clone({
      setHeaders: {
        Authorization: `Bearer ${accessToken}`
      }
    });
  }

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {

      // Se for 401 E já temos um token (usuário estava logado) E não é uma requisição de auth
      if (error.status === 401 &&
          accessToken &&
          !req.url.includes('/auth/') &&
          auth.isTokenExpirado()) {

        return handle401Error(req, next, auth, router);
      }

      // Se for 401 em uma rota de auth, simplesmente propagamos o erro
      if (error.status === 401 && req.url.includes('/auth/')) {
        return throwError(() => error);
      }

      // Outros erros
      return throwError(() => error);
    })
  );
}

function handle401Error(
  request: HttpRequest<any>,
  next: HttpHandlerFn,
  auth: AuthService,
  router: Router
): Observable<HttpEvent<any>> {

  if (!isRefreshing) {
    isRefreshing = true;
    refreshTokenSubject.next(null);

    return auth.rotacionarToken().pipe(
      switchMap((token: any) => {
        isRefreshing = false;
        refreshTokenSubject.next(token.accessToken);

        // Reexecuta a requisição original com o novo token
        const newRequest = request.clone({
          setHeaders: {
            Authorization: `Bearer ${token.accessToken}`
          }
        });

        return next(newRequest);
      }),
      catchError(err => {
        isRefreshing = false;

        // Se falhar no refresh, faz logout
        auth.limparStorage();
        router.navigate(['/login']);
        return throwError(() => err);
      })
    );

  } else {
    // Se já está fazendo refresh, espera o token ser atualizado
    return refreshTokenSubject.pipe(
      filter(token => token != null),
      take(1),
      switchMap(token => {
        const newRequest = request.clone({
          setHeaders: {
            Authorization: `Bearer ${token!}`
          }
        });
        return next(newRequest);
      })
    );
  }
}
