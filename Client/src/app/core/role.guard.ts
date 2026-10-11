import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { NotificacaoService } from './notificacao.service';
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
  const notificacao = inject(NotificacaoService);

  const papeis = route.data['roles'] as readonly string[] | undefined;

  return auth.garantirSessao().pipe(
    map(logado => {
      // Sem sessão quem decide é o AuthGuard (manda para o login); aqui não atrapalha.
      if (!logado || cargoPodeAcessar(auth.getUsuario()?.cargo, papeis)) {
        return true;
      }

      notificacao.aviso('Você não tem permissão para acessar esta página.');
      return router.createUrlTree(['/home']);
    })
  );
};
