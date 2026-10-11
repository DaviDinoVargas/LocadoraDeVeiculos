import { HttpContextToken } from '@angular/common/http';

// Requisições que tratam o próprio erro na tela (ex.: a simulação de preço do aluguel dispara a
// cada mudança de campo) usam este token para o interceptor global NÃO abrir um snackbar a
// cada falha:  http.post(url, body, { context: new HttpContext().set(SILENCIAR_ERRO, true) })
export const SILENCIAR_ERRO = new HttpContextToken<boolean>(() => false);
