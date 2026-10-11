import { Injectable } from '@angular/core';
import { CanActivate, Router, ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {
  constructor(private auth: AuthService, private router: Router) {}

  canActivate(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<boolean | UrlTree> {
    // O access token vive só em memória, então some a cada F5/nova aba. Antes de mandar
    // para o login, tenta restaurar a sessão com o refresh token (cookie HttpOnly).
    return this.auth.garantirSessao().pipe(
      map(sucesso => sucesso
        ? true
        : this.router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } })
      )
    );
  }
}
