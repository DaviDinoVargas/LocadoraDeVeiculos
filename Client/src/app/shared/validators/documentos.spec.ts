import { FormControl } from '@angular/forms';
import { cnpjValidator, cnpjValido, cpfValidator, cpfValido, PADRAO_CNPJ, PADRAO_CPF, somenteDigitos } from './documentos';

describe('validação de CPF', () => {
  it('aceita CPFs com dígitos verificadores corretos, com ou sem pontuação', () => {
    expect(cpfValido('529.982.247-25')).toBeTrue();
    expect(cpfValido('52998224725')).toBeTrue();
    expect(cpfValido('111.444.777-35')).toBeTrue();
    expect(cpfValido('123.456.789-09')).toBeTrue();
  });

  it('recusa dígito verificador errado (o regex de formato antigo aceitava)', () => {
    expect(cpfValido('529.982.247-26')).toBeFalse();
    expect(cpfValido('123.456.789-00')).toBeFalse();
    expect(cpfValido('111.222.333-44')).toBeFalse();
  });

  it('recusa sequências repetidas, que passam na conta dos dígitos', () => {
    for (let d = 0; d <= 9; d++) {
      expect(cpfValido(String(d).repeat(11))).withContext(`${d} x 11`).toBeFalse();
    }
  });

  it('recusa tamanho errado, vazio e nulo', () => {
    expect(cpfValido('5299822472')).toBeFalse();
    expect(cpfValido('529982247250')).toBeFalse();
    expect(cpfValido('')).toBeFalse();
    expect(cpfValido(null)).toBeFalse();
    expect(cpfValido(undefined)).toBeFalse();
  });
});

describe('validação de CNPJ', () => {
  it('aceita CNPJs com dígitos verificadores corretos, com ou sem pontuação', () => {
    expect(cnpjValido('11.222.333/0001-81')).toBeTrue();
    expect(cnpjValido('11222333000181')).toBeTrue();
    expect(cnpjValido('11.444.777/0001-61')).toBeTrue();
    expect(cnpjValido('12.345.678/0001-95')).toBeTrue();
  });

  it('recusa dígito verificador errado', () => {
    expect(cnpjValido('11.222.333/0001-82')).toBeFalse();
    expect(cnpjValido('12.345.678/0001-99')).toBeFalse();
  });

  it('recusa sequências repetidas e tamanho errado', () => {
    for (let d = 0; d <= 9; d++) {
      expect(cnpjValido(String(d).repeat(14))).withContext(`${d} x 14`).toBeFalse();
    }
    expect(cnpjValido('1122233300018')).toBeFalse();
    expect(cnpjValido('')).toBeFalse();
  });
});

describe('validators de formulário', () => {
  it('cpfValidator: vazio é do Validators.required; inválido devolve cpfInvalido; válido passa', () => {
    expect(cpfValidator(new FormControl(''))).toBeNull();
    expect(cpfValidator(new FormControl('529.982.247-26'))).toEqual({ cpfInvalido: true });
    expect(cpfValidator(new FormControl('529.982.247-25'))).toBeNull();
  });

  it('cnpjValidator: vazio é do Validators.required; inválido devolve cnpjInvalido; válido passa', () => {
    expect(cnpjValidator(new FormControl(null))).toBeNull();
    expect(cnpjValidator(new FormControl('11.222.333/0001-82'))).toEqual({ cnpjInvalido: true });
    expect(cnpjValidator(new FormControl('11.222.333/0001-81'))).toBeNull();
  });

  it('os padrões de formato são os mesmos que a API exige', () => {
    expect(PADRAO_CPF.test('529.982.247-25')).toBeTrue();
    expect(PADRAO_CPF.test('52998224725')).toBeFalse(); // a API recusava sem pontuação
    expect(PADRAO_CNPJ.test('11.222.333/0001-81')).toBeTrue();
    expect(PADRAO_CNPJ.test('11222333000181')).toBeFalse();
  });

  it('somenteDigitos remove tudo que não é número', () => {
    expect(somenteDigitos('529.982.247-25')).toBe('52998224725');
    expect(somenteDigitos(null)).toBe('');
  });
});
