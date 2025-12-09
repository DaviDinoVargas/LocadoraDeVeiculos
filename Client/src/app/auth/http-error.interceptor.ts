import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError } from 'rxjs/operators';
import { throwError } from 'rxjs';
import { MatSnackBar } from '@angular/material/snack-bar';

export const HttpErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const snackBar = inject(MatSnackBar);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      console.error('HTTP Error:', {
        url: req.url,
        status: error.status,
        statusText: error.statusText,
        error: error.error,
        headers: error.headers
      });

      let errorMessage = 'Erro na requisição';

      if (error.error instanceof ErrorEvent) {
        // Erro do cliente
        errorMessage = `Erro: ${error.error.message}`;
      } else {
        // Erro do servidor
        if (error.status === 400) {
          // Erro de validação
          if (typeof error.error === 'string') {
            errorMessage = error.error;
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

      snackBar.open(errorMessage, 'Fechar', {
        duration: 5000,
        horizontalPosition: 'center',
        verticalPosition: 'top'
      });

      return throwError(() => error);
    })
  );
};
