import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';
import { Router } from '@angular/router';
import { Observable, BehaviorSubject, throwError } from 'rxjs';
import { catchError, filter, switchMap, take } from 'rxjs/operators';

let isRefreshing = false;
const refreshTokenSubject = new BehaviorSubject<string | null>(null);

export const AuthInterceptor: HttpInterceptorFn = (req, next) => {

  const auth = inject(AuthService);
  const router = inject(Router);

  const accessToken = auth.getAccessToken();

  let authReq = req;

  if (accessToken) {
    authReq = req.clone({
      setHeaders: { Authorization: `Bearer ${accessToken}` }
    });
  }

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {

      if (error.status === 401) {
        return handle401Error(authReq, next, auth, router);
      }

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

        return next(
          request.clone({
            setHeaders: { Authorization: `Bearer ${token.accessToken}` }
          })
        );
      }),
      catchError(err => {
        isRefreshing = false;

        auth.limparStorage();
        router.navigate(['/login']);
        return throwError(() => err);
      })
    );

  } else {
    return refreshTokenSubject.pipe(
      filter(token => token != null),
      take(1),
      switchMap(token => {
        return next(
          request.clone({
            setHeaders: { Authorization: `Bearer ${token!}` }
          })
        );
      })
    );
  }
}
