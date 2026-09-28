import { tipoDescontoLabel } from './cupom.model';

describe('tipoDescontoLabel', () => {
  it('traduz os dois tipos de desconto conhecidos', () => {
    expect(tipoDescontoLabel('Percentual')).toBe('Percentual (%)');
    expect(tipoDescontoLabel('ValorFixo')).toBe('Valor fixo (R$)');
  });

  it('devolve o valor original quando o tipo não é reconhecido', () => {
    expect(tipoDescontoLabel('Desconhecido')).toBe('Desconhecido');
  });
});
