import { violacoesDeAcessibilidade, violacoesNosDoisTemas } from './acessibilidade';

describe('Auditor de acessibilidade (axe-core) — o próprio auditor encontra problemas reais', () => {
  let area: HTMLElement;

  const montar = (html: string) => {
    area = document.createElement('div');
    area.innerHTML = html;
    document.body.appendChild(area);
    return area;
  };

  afterEach(() => area?.remove());

  it('acusa botão sem nome acessível', async () => {
    const violacoes = await violacoesDeAcessibilidade(montar('<button type="button"></button>'));

    expect(violacoes.some(v => v.includes('button-name'))).toBeTrue();
  });

  it('acusa texto sem contraste suficiente (usa as cores renderizadas de verdade)', async () => {
    const violacoes = await violacoesDeAcessibilidade(montar('<p style="color:#cccccc;background:#ffffff">texto claro demais</p>'));

    expect(violacoes.some(v => v.includes('color-contrast'))).toBeTrue();
  });

  it('acusa campo sem rótulo e imagem sem alternativa', async () => {
    const violacoes = await violacoesDeAcessibilidade(montar('<input type="text"><img src="data:image/gif;base64,R0lGODlhAQABAAAAACw=">'));

    expect(violacoes.some(v => v.includes('label'))).toBeTrue();
    expect(violacoes.some(v => v.includes('image-alt'))).toBeTrue();
  });

  it('marcação correta não gera violações e o tema escuro volta ao estado anterior', async () => {
    const antes = document.documentElement.classList.contains('dark');

    const violacoes = await violacoesNosDoisTemas(montar('<label>Nome <input type="text"></label><button type="button">Salvar</button>'));

    expect(violacoes).toEqual([]);
    expect(document.documentElement.classList.contains('dark')).toBe(antes);
  });

  it('desligar uma regra a tira do relatório (para exceções justificadas)', async () => {
    const violacoes = await violacoesDeAcessibilidade(montar('<button type="button"></button>'), 'claro', { desligar: ['button-name'] });

    expect(violacoes.some(v => v.includes('button-name'))).toBeFalse();
  });
});
