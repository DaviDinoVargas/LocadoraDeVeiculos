import { FormControl, Validators } from '@angular/forms';
import { MENSAGENS_SENHA, TAMANHO_MAXIMO_SENHA, TAMANHO_MINIMO_SENHA, VALIDADORES_SENHA } from './senha';

describe('Política de senha (a mesma da API)', () => {
  const erros = (valor: string) => new FormControl(valor, [Validators.required, ...VALIDADORES_SENHA]).errors ?? {};

  it('aceita uma senha forte dentro dos limites', () => {
    expect(erros('Senha@12345')).toEqual({});
    expect(erros('Aa1' + 'x'.repeat(TAMANHO_MAXIMO_SENHA - 3))).toEqual({});
  });

  it('recusa menos de 8 caracteres e informa o motivo', () => {
    expect(erros('Aa1')['minlength']).toBeTruthy();
    expect(MENSAGENS_SENHA['minlength']).toContain(String(TAMANHO_MINIMO_SENHA));
  });

  it('recusa mais de 128 caracteres (teto da API) com mensagem própria', () => {
    expect(erros('Aa1' + 'x'.repeat(TAMANHO_MAXIMO_SENHA))['maxlength']).toBeTruthy();
    expect(MENSAGENS_SENHA['maxlength']).toContain('128');
  });

  it('exige maiúscula, minúscula e número', () => {
    expect(erros('senha@12345')['semMaiuscula']).toBeTrue();
    expect(erros('SENHA@12345')['semMinuscula']).toBeTrue();
    expect(erros('Senha@abcde')['semNumero']).toBeTrue();
  });
});
