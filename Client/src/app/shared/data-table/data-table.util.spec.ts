import { campoCsv, compararValores, formatarData, formatarDataHora, formatarMoeda, formatarNumero, normalizar } from './data-table.util';

describe('data-table.util', () => {
  it('moeda e número no formato pt-BR; vazio vira "—"', () => {
    expect(formatarMoeda(1080)).toMatch(/^R\$\s1\.080,00$/);
    expect(formatarMoeda(null)).toBe('—');
    expect(formatarMoeda('abc')).toBe('—');
    expect(formatarNumero(1234567)).toBe('1.234.567');
    expect(formatarNumero(undefined)).toBe('—');
  });

  it('data "YYYY-MM-DD" não desloca um dia por fuso', () => {
    expect(formatarData('2026-10-11')).toBe('11/10/2026');
    expect(formatarData('2026-01-01')).toBe('01/01/2026');
  });

  it('data com horário e valores inválidos', () => {
    expect(formatarData('2026-10-11T15:30:00')).toBe('11/10/2026');
    expect(formatarData('')).toBe('—');
    expect(formatarData('não é data')).toBe('—');
    expect(formatarDataHora(null)).toBe('—');
    expect(formatarDataHora('2026-10-11T15:30:00')).toMatch(/11\/10\/(20)?26/);
  });

  it('normalizar tira acentos e caixa para a busca tolerante', () => {
    expect(normalizar('  José da Conceição ')).toBe('jose da conceicao');
    expect(normalizar(null)).toBe('');
  });

  it('compararValores: números pela grandeza, textos pelo alfabeto pt-BR, vazios no fim', () => {
    expect(compararValores(2, 10)).toBeLessThan(0);
    expect(compararValores('Ônix', 'Uno')).toBeLessThan(0);
    expect(compararValores('item 2', 'item 10')).toBeLessThan(0); // numérico: 2 antes de 10
    expect(compararValores(null, 'a')).toBeGreaterThan(0);
    expect(compararValores('a', undefined)).toBeLessThan(0);
    expect(compararValores('', '')).toBe(0);
    expect(compararValores(true, false)).toBeGreaterThan(0);
  });

  it('CSV: escapa aspas, ponto e vírgula e quebras de linha', () => {
    expect(campoCsv('simples')).toBe('simples');
    expect(campoCsv('a;b')).toBe('"a;b"');
    expect(campoCsv('diz "oi"')).toBe('"diz ""oi"""');
    expect(campoCsv('linha1\nlinha2')).toBe('"linha1\nlinha2"');
  });
});
