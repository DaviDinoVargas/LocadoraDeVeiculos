import { Injectable } from '@angular/core';
import { CanActivate, Router, ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {
  constructor(private auth: AuthService, private router: Router) {}

  canActivate(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): boolean | UrlTree {
    console.log('AuthGuard executando...');
    console.log('isLoggedIn:', this.auth.isLoggedIn());
    console.log('Token:', this.auth.getAccessToken());
    console.log('Token expirado:', this.auth.isTokenExpirado());
    console.log('Usuário:', this.auth.getUsuario());

    if (this.auth.isLoggedIn()) {
      console.log('Usuário autenticado, permitindo acesso a:', state.url);
      return true;
    }

    console.log('Usuário não autenticado, redirecionando para login');
    return this.router.createUrlTree(['/login'], {
      queryParams: { returnUrl: state.url }
    });
  }
}
