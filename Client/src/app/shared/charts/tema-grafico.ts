/** Cores e tipografia dos gráficos, lidas dos tokens CSS (nenhum hex dentro da configuração do gráfico). */
export interface TemaGrafico {
  series: string[];
  texto: string;
  textoSecundario: string;
  grade: string;
  fundoDica: string;
  textoDica: string;
  fonte: string;
}

function token(nome: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(nome).trim();
}

/** Lê as cores atuais (claro ou escuro). Chamar de novo quando o tema mudar. */
export function lerTemaGrafico(): TemaGrafico {
  return {
    series: ['--chart-1', '--chart-2', '--chart-3', '--chart-4', '--chart-5'].map(token),
    texto: token('--foreground'),
    textoSecundario: token('--muted-foreground'),
    grade: token('--border'),
    fundoDica: token('--popover'),
    textoDica: token('--popover-foreground'),
    fonte: getComputedStyle(document.body).fontFamily
  };
}
