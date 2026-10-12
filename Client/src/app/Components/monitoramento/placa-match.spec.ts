import { casarPlaca, normalizarPlaca } from './placa-match';

describe('placa-match — casa a placa lida pelo OCR com as dos aluguéis em aberto', () => {
  const cadastradas = ['BRA2E19', 'ABC1D30'];

  it('normaliza hífen, espaço e minúsculas', () => {
    expect(normalizarPlaca('bra-2e19 ')).toBe('BRA2E19');
    expect(normalizarPlaca(null)).toBe('');
  });

  it('placa igual casa (mesmo com hífen/minúsculas)', () => {
    expect(casarPlaca('BRA2E19', cadastradas)).toBe('BRA2E19');
    expect(casarPlaca('bra-2e19', cadastradas)).toBe('BRA2E19');
  });

  it('R lido como K (caso real da câmera) casa com a cadastrada', () => {
    expect(casarPlaca('BKA2E19', cadastradas)).toBe('BRA2E19');
  });

  it('outras confusões típicas de 1 caractere casam: B/8, O/0, I/1', () => {
    expect(casarPlaca('8RA2E19', cadastradas)).toBe('BRA2E19');
    expect(casarPlaca('ABC1D3O', cadastradas)).toBe('ABC1D30');
    expect(casarPlaca('ABCID30', cadastradas)).toBe('ABC1D30');
  });

  it('a leitura exata tem prioridade sobre a aproximada', () => {
    expect(casarPlaca('BKA2E19', ['BRA2E19', 'BKA2E19'])).toBe('BKA2E19');
  });

  it('não adivinha: 2 caracteres diferentes, diferença que não é confusão ou tamanho errado', () => {
    expect(casarPlaca('BKA2E10', cadastradas)).toBeNull();   // K e 0 diferem (2 trocas)
    expect(casarPlaca('BWA2E19', cadastradas)).toBeNull();   // W não é confundível com R
    expect(casarPlaca('BRA2E1', cadastradas)).toBeNull();    // tamanho diferente
    expect(casarPlaca('', cadastradas)).toBeNull();
  });

  it('com mais de uma candidata possível é ambíguo e não casa', () => {
    expect(casarPlaca('BRA2E19', ['BKA2E19', 'BPA2E19'])).toBeNull();   // R→K e R→P são confusões plausíveis
  });
});
