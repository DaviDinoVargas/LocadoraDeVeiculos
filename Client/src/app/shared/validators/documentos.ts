import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';

// Os formatos abaixo são os que a Web API exige (FluentValidation: Matches nos validators de
// cliente, condutor, funcionário e parceiro). Os campos usam a máscara de digitação
// (MascaraDirective), que sempre produz esse formato.
export const PADRAO_CPF = /^\d{3}\.\d{3}\.\d{3}-\d{2}$/;
export const PADRAO_CNPJ = /^\d{2}\.\d{3}\.\d{3}\/\d{4}-\d{2}$/;

export function somenteDigitos(valor: string | null | undefined): string {
  return (valor ?? '').replace(/\D/g, '');
}

function todosIguais(digitos: string): boolean {
  return /^(\d)\1+$/.test(digitos);
}

// Dígito verificador: soma ponderada dos dígitos, resto da divisão por 11 (módulo 11).
function digitoVerificador(digitos: string, pesos: number[]): number {
  const soma = pesos.reduce((total, peso, i) => total + Number(digitos[i]) * peso, 0);
  const resto = soma % 11;
  return resto < 2 ? 0 : 11 - resto;
}

/** CPF com 11 dígitos, que não seja uma sequência repetida (111.111.111-11) e com os 2 dígitos verificadores corretos. */
export function cpfValido(valor: string | null | undefined): boolean {
  const cpf = somenteDigitos(valor);
  if (cpf.length !== 11 || todosIguais(cpf)) return false;

  const d1 = digitoVerificador(cpf, [10, 9, 8, 7, 6, 5, 4, 3, 2]);
  const d2 = digitoVerificador(cpf, [11, 10, 9, 8, 7, 6, 5, 4, 3, 2]);
  return d1 === Number(cpf[9]) && d2 === Number(cpf[10]);
}

/** CNPJ com 14 dígitos, que não seja uma sequência repetida e com os 2 dígitos verificadores corretos. */
export function cnpjValido(valor: string | null | undefined): boolean {
  const cnpj = somenteDigitos(valor);
  if (cnpj.length !== 14 || todosIguais(cnpj)) return false;

  const d1 = digitoVerificador(cnpj, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
  const d2 = digitoVerificador(cnpj, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
  return d1 === Number(cnpj[12]) && d2 === Number(cnpj[13]);
}

// Campo vazio é problema do Validators.required, não deste validador (erro único e claro por causa).
export const cpfValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null =>
  !control.value || cpfValido(control.value) ? null : { cpfInvalido: true };

export const cnpjValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null =>
  !control.value || cnpjValido(control.value) ? null : { cnpjInvalido: true };

export const VALIDADORES_CPF: ValidatorFn[] = [Validators.required, Validators.pattern(PADRAO_CPF), cpfValidator];
export const VALIDADORES_CNPJ: ValidatorFn[] = [Validators.required, Validators.pattern(PADRAO_CNPJ), cnpjValidator];

// Mensagens específicas dos campos de documento para o `app-form-field` ([mensagens]).
export const MENSAGENS_CPF = {
  pattern: 'Use o formato 000.000.000-00.',
  cpfInvalido: 'CPF inválido: confira os números.'
};
export const MENSAGENS_CNPJ = {
  pattern: 'Use o formato 00.000.000/0000-00.',
  cnpjInvalido: 'CNPJ inválido: confira os números.'
};
