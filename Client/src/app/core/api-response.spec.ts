import { extrairDados } from './api-response';

describe('extrairDados', () => {
  it('lista: devolve o conteúdo de "registros" (resposta plana da Web API)', () => {
    expect(extrairDados<number[]>({ registros: [1, 2, 3] })).toEqual([1, 2, 3]);
  });

  it('lista vazia continua sendo lista vazia', () => {
    expect(extrairDados<number[]>({ registros: [] })).toEqual([]);
  });

  it('objeto simples (detalhe, configuração) é devolvido como veio', () => {
    const configuracao = { id: '1', precoCombustivel: 5.5 };

    expect(extrairDados(configuracao)).toBe(configuracao);
  });

  it('o antigo wrapper { dados } não é mais desembrulhado: foi removido do backend', () => {
    const embrulho = { dados: [1] };

    expect(extrairDados(embrulho)).toBe(embrulho);
  });

  it('null, undefined e vazio passam sem erro (ex.: 204 de DELETE)', () => {
    expect(extrairDados(null)).toBeNull();
    expect(extrairDados(undefined)).toBeUndefined();
    expect(extrairDados('')).toBe('');
  });
});
