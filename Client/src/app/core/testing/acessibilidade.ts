import axe from 'axe-core';

/** Regras de página inteira que não fazem sentido ao auditar um componente isolado (o conjunto da página tem `main`, `h1`...). */
const REGRAS_DE_PAGINA = ['region', 'landmark-one-main', 'page-has-heading-one', 'landmark-unique', 'landmark-no-duplicate-banner'];

export type TemaDeAuditoria = 'claro' | 'escuro';

export interface OpcoesAuditoria {
  /** Regras do axe a desligar, sempre com o motivo no comentário de quem chama. */
  desligar?: string[];
}

/** Aplica o tema como o `TemaService` faz (classe `dark` no <html>) e devolve como estava, para o próximo teste. */
async function noTema<T>(tema: TemaDeAuditoria, tarefa: () => Promise<T>): Promise<T> {
  const html = document.documentElement;
  const tinhaDark = html.classList.contains('dark');

  // O app anima as cores (`transition-colors`, 150 ms). Sem desligar isso, o axe lê o fundo no meio da transição
  // (texto já escuro sobre fundo ainda claro) e acusa contraste que o usuário nunca vê.
  const semTransicao = document.createElement('style');
  semTransicao.textContent = '*, *::before, *::after { transition: none !important; animation: none !important; }';
  document.head.appendChild(semTransicao);

  html.classList.toggle('dark', tema === 'escuro');
  await new Promise<void>(resolver => requestAnimationFrame(() => requestAnimationFrame(() => resolver())));
  try {
    return await tarefa();
  } finally {
    html.classList.toggle('dark', tinhaDark);
    semTransicao.remove();
  }
}

/**
 * Roda o axe-core (WCAG 2.0/2.1/2.2 A e AA + boas práticas) no elemento, **no Edge real e com o `styles.css` carregado**,
 * então contraste de cor e nome acessível valem de verdade. Devolve uma linha legível por violação (vazio = sem violações).
 */
export async function violacoesDeAcessibilidade(
  raiz: HTMLElement,
  tema: TemaDeAuditoria = 'claro',
  opcoes: OpcoesAuditoria = {}
): Promise<string[]> {
  return noTema(tema, async () => {
    const regras = Object.fromEntries([...REGRAS_DE_PAGINA, ...(opcoes.desligar ?? [])].map(id => [id, { enabled: false }]));
    const resultado = await axe.run(raiz, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa', 'best-practice'] },
      rules: regras
    });
    return resultado.violations.map(v => {
      const alvos = v.nodes.slice(0, 4).map(n => {
        // contraste: mostra as cores e a razão medida, que é o que se precisa para corrigir (e para a documentação)
        const dados = n.any.find(c => c.id === 'color-contrast')?.data as { fgColor?: string; bgColor?: string; contrastRatio?: number; expectedContrastRatio?: string } | undefined;
        const medida = dados ? ` {texto ${dados.fgColor}, fundo ${dados.bgColor}, razão ${dados.contrastRatio}, mínimo ${dados.expectedContrastRatio}}` : '';
        return n.target.join(' ') + medida;
      }).join(' | ');
      return `[${tema}] ${v.id} (${v.impact}): ${v.help} — ${v.nodes.length} elemento(s): ${alvos}`;
    });
  });
}

/** Audita nos dois temas (claro e escuro) e junta as violações. */
export async function violacoesNosDoisTemas(raiz: HTMLElement, opcoes: OpcoesAuditoria = {}): Promise<string[]> {
  return [...(await violacoesDeAcessibilidade(raiz, 'claro', opcoes)), ...(await violacoesDeAcessibilidade(raiz, 'escuro', opcoes))];
}
