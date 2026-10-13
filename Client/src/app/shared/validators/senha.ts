import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';

// Mesma política da Web API (Identity + FluentValidation): 8+ caracteres, maiúscula, minúscula e número.
// Campo vazio é problema do Validators.required, não deste validador (erro único e claro por causa).
export const TAMANHO_MINIMO_SENHA = 8;
export const TAMANHO_MAXIMO_SENHA = 128; // o mesmo teto da API (RegrasDeSenha.TamanhoMaximo)

export const senhaForteValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const valor: string = control.value ?? '';
  if (!valor) return null;
  const erros: ValidationErrors = {};
  if (!/[A-Z]/.test(valor)) erros['semMaiuscula'] = true;
  if (!/[a-z]/.test(valor)) erros['semMinuscula'] = true;
  if (!/[0-9]/.test(valor)) erros['semNumero'] = true;
  return Object.keys(erros).length ? erros : null;
};

export const VALIDADORES_SENHA: ValidatorFn[] = [Validators.minLength(TAMANHO_MINIMO_SENHA), Validators.maxLength(TAMANHO_MAXIMO_SENHA), senhaForteValidator];

export const MENSAGENS_SENHA = {
  minlength: `A senha precisa de ao menos ${TAMANHO_MINIMO_SENHA} caracteres.`,
  maxlength: `A senha pode ter no máximo ${TAMANHO_MAXIMO_SENHA} caracteres.`,
  semMaiuscula: 'Use ao menos uma letra maiúscula.',
  semMinuscula: 'Use ao menos uma letra minúscula.',
  semNumero: 'Use ao menos um número.'
};
