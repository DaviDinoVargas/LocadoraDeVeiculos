import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { AbstractControl } from '@angular/forms';
import { ConfirmacaoService } from './confirmacao.service';

/** Componentes de formulário que expõem o `form` participam da proteção contra perda de alterações. */
export interface FormProtegido {
  form: AbstractControl;
}

/**
 * `canDeactivate` das rotas de formulário: se há alterações não salvas, pergunta antes de sair.
 * O formulário chama `form.markAsPristine()` ao salvar com sucesso, então salvar e navegar não pergunta.
 */
export const formularioSemPerdaGuard: CanDeactivateFn<FormProtegido> = componente => {
  if (!componente.form?.dirty) return true;
  return inject(ConfirmacaoService).confirmar({
    titulo: 'Descartar alterações?',
    mensagem: 'Você tem alterações não salvas neste formulário. Se sair agora, elas serão perdidas.',
    confirmar: 'Descartar e sair',
    cancelar: 'Continuar editando',
    destrutivo: true
  });
};
