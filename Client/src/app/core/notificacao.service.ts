import { Injectable } from '@angular/core';
import { toast } from 'ngx-sonner';

/**
 * Toasts do app (ngx-sonner, o padrão do Spartan). Substitui o MatSnackBar: confirmações efêmeras
 * de 4–6 s; erros ficam um pouco mais. O host `<hlm-toaster />` vive no app-shell/app.
 */
@Injectable({ providedIn: 'root' })
export class NotificacaoService {
  sucesso(mensagem: string): void {
    toast.success(mensagem, { duration: 4000 });
  }

  erro(mensagem: string): void {
    toast.error(mensagem, { duration: 6000 });
  }

  aviso(mensagem: string): void {
    toast.warning(mensagem, { duration: 5000 });
  }

  info(mensagem: string): void {
    toast.info(mensagem, { duration: 4000 });
  }
}
