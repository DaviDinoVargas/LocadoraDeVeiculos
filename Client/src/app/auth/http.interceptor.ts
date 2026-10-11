import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { NotificacaoService } from '../core/notificacao.service';
import { SILENCIAR_ERRO } from '../core/http-context';
import { mensagensDeErro } from '../core/api-error';

// Interceptor de Erro HTTP
export const HttpErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const notificacao = inject(NotificacaoService);

  if (req.context.get(SILENCIAR_ERRO)) {
    return next(req);
  }

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      let errorMessage = 'Erro na requisição';

      if (error.error instanceof ErrorEvent) {
        // Erro do cliente
        errorMessage = `Erro: ${error.error.message}`;
      } else {
        // Erro do servidor
        if (error.status === 409 || error.status === 422 || error.status === 404) {
          // Erros de negócio vêm do servidor como array de mensagens (ver core/api-error.ts)
          const mensagens = mensagensDeErro(error, '');
          errorMessage = mensagens[0] || (error.status === 404 ? 'Recurso não encontrado.' : 'Operação não permitida.');
        } else if (error.status === 400) {
          // Erro de validação
          if (Array.isArray(error.error) || typeof error.error === 'string') {
            errorMessage = mensagensDeErro(error).join(' ');
          } else if (error.error?.errors) {
            // Para erros de validação do ASP.NET
            const validationErrors = error.error.errors;
            errorMessage = Object.values(validationErrors).flat().join(', ');
          } else if (error.error?.message) {
            errorMessage = error.error.message;
          } else {
            errorMessage = 'Dados inválidos. Verifique os campos preenchidos.';
          }
        } else if (error.status === 401) {
          errorMessage = 'Não autorizado. Faça login novamente.';
        } else if (error.status === 403) {
          errorMessage = 'Acesso negado.';
        } else if (error.status === 404) {
          errorMessage = 'Recurso não encontrado.';
        } else if (error.status === 500) {
          errorMessage = 'Erro interno do servidor.';
        }
      }

      notificacao.erro(errorMessage);

      return throwError(() => error);
    })
  );
};
