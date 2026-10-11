import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { map } from 'rxjs/operators';
import { AuthService } from '../auth/auth.service';
import { cargoPodeAcessar } from './permissoes';

// Use junto do AuthGuard: canActivate: [AuthGuard, roleGuard] e data: { roles: SOMENTE_EMPRESA }.
// Rota sem data.roles continua liberada para qualquer usuário autenticado.
//
// Atenção: o Angular executa os guards de UMA MESMA rota em paralelo, então este guard não pode
// supor que o AuthGuard já restaurou a sessão (após um F5 o usuário ainda não existe). Por isso
// ele próprio espera garantirSessao(), que compartilha a restauração com o AuthGuard.
export const roleGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const snack = inject(MatSnackBar);

  const papeis = route.data['roles'] as readonly string[] | undefined;

  return auth.garantirSessao().pipe(
    map(logado => {
      // Sem sessão quem decide é o AuthGuard (manda para o login); aqui não atrapalha.
      if (!logado || cargoPodeAcessar(auth.getUsuario()?.cargo, papeis)) {
        return true;
      }

      snack.open('Você não tem permissão para acessar esta página.', 'Fechar', { duration: 4000 });
      return router.createUrlTree(['/home']);
    })
  );
};
