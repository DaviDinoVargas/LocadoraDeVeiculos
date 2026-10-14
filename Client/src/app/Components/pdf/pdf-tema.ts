import { jsPDF } from 'jspdf';
import type { CellHookData, UserOptions } from 'jspdf-autotable';

type Rgb = [number, number, number];

const rgb = (hex: string): Rgb => [
  parseInt(hex.slice(1, 3), 16),
  parseInt(hex.slice(3, 5), 16),
  parseInt(hex.slice(5, 7), 16)
];

/**
 * Cores dos PDFs: os mesmos papéis do tema CLARO do app (`:root` em `src/styles.css`). O papel é sempre branco e
 * impresso em claro, então não se leem os tokens do tema ativo (no escuro sairiam cores de tela escura). Se a
 * marca mudar em `styles.css`, atualize aqui. O jsPDF só aceita RGB, por isso os valores ficam concentrados neste arquivo.
 */
export const COR = {
  primaria: rgb('#6750a4'),
  sobrePrimaria: rgb('#ffffff'),
  containerPrimaria: rgb('#eaddff'),
  sobreContainerPrimaria: rgb('#21005d'),
  containerSecundaria: rgb('#e8def8'),
  sobreContainerSecundaria: rgb('#1d192b'),
  texto: rgb('#1d1b20'),
  textoSecundario: rgb('#49454f'),
  superficieClara: rgb('#ffffff'),
  superficieBaixa: rgb('#f7f2fa'),
  superficie: rgb('#f3edf7'),
  linha: rgb('#cac4d0'),
  contorno: rgb('#79747e'),
  containerSucesso: rgb('#b7f2c5'),
  sobreContainerSucesso: rgb('#00210d'),
  containerAlerta: rgb('#ffddb8'),
  sobreContainerAlerta: rgb('#2c1600'),
  containerErro: rgb('#f9dedc'),
  sobreContainerErro: rgb('#410e0b')
} as const;

/** Mesmos tons do selo de status das telas (`app-status-badge`): info, alerta, sucesso e erro. */
export type TomSelo = 'info' | 'warning' | 'success' | 'error' | 'neutral';

export const SELO: Record<TomSelo, { fundo: Rgb; texto: Rgb }> = {
  info: { fundo: COR.containerPrimaria, texto: COR.sobreContainerPrimaria },
  warning: { fundo: COR.containerAlerta, texto: COR.sobreContainerAlerta },
  success: { fundo: COR.containerSucesso, texto: COR.sobreContainerSucesso },
  error: { fundo: COR.containerErro, texto: COR.sobreContainerErro },
  neutral: { fundo: COR.superficie, texto: COR.textoSecundario }
};

export const MARGEM = 14;

/** Marca do app: círculo na cor primária com o carro (o mesmo ícone da barra lateral), desenhado com formas simples. */
function marca(doc: jsPDF, cx: number, cy: number): void {
  doc.setFillColor(...COR.primaria);
  doc.circle(cx, cy, 5.2, 'F');

  doc.setFillColor(...COR.sobrePrimaria);
  doc.roundedRect(cx - 3.4, cy - 0.3, 6.8, 2.4, 0.9, 0.9, 'F'); // carroceria
  doc.roundedRect(cx - 2.1, cy - 2, 4.2, 2, 0.7, 0.7, 'F'); // cabine
  doc.setFillColor(...COR.primaria);
  doc.circle(cx - 2.1, cy + 2.2, 1.1, 'F'); // rodas (aro na cor do fundo para recortar o desenho)
  doc.circle(cx + 2.1, cy + 2.2, 1.1, 'F');
  doc.setFillColor(...COR.sobrePrimaria);
  doc.circle(cx - 2.1, cy + 2.2, 0.75, 'F');
  doc.circle(cx + 2.1, cy + 2.2, 0.75, 'F');
}

/** Cabeçalho da primeira página (marca, nome, título, subtítulo e data de geração). Devolve o `y` onde o conteúdo começa. */
export function cabecalho(doc: jsPDF, titulo: string, subtitulo: string): number {
  const largura = doc.internal.pageSize.getWidth();
  const agora = new Date();

  marca(doc, MARGEM + 5.2, 15);

  doc.setFont('helvetica', 'bold');
  doc.setFontSize(11);
  doc.setTextColor(...COR.texto);
  doc.text('Locadora de Veículos', MARGEM + 13, 16.2);

  doc.setFont('helvetica', 'normal');
  doc.setFontSize(8.5);
  doc.setTextColor(...COR.textoSecundario);
  doc.text(`Gerado em ${agora.toLocaleDateString('pt-BR')} às ${agora.toLocaleTimeString('pt-BR')}`, largura - MARGEM, 16.2, { align: 'right' });

  doc.setFont('helvetica', 'bold');
  doc.setFontSize(18);
  doc.setTextColor(...COR.texto);
  doc.text(titulo, MARGEM, 31);

  doc.setFont('helvetica', 'normal');
  doc.setFontSize(9.5);
  doc.setTextColor(...COR.textoSecundario);
  doc.text(subtitulo, MARGEM, 37.5);

  doc.setDrawColor(...COR.linha);
  doc.setLineWidth(0.3);
  doc.line(MARGEM, 42, largura - MARGEM, 42);

  return 48;
}

/** Cartões de resumo lado a lado (como os KPIs do painel). Devolve o `y` logo abaixo deles. */
export function cartoesResumo(doc: jsPDF, y: number, itens: { rotulo: string; valor: string }[]): number {
  const largura = doc.internal.pageSize.getWidth();
  const espaco = 4;
  const largCartao = (largura - 2 * MARGEM - espaco * (itens.length - 1)) / itens.length;
  const altura = 17;

  itens.forEach((item, i) => {
    const x = MARGEM + i * (largCartao + espaco);
    doc.setFillColor(...COR.superficieBaixa);
    doc.setDrawColor(...COR.linha);
    doc.setLineWidth(0.25);
    doc.roundedRect(x, y, largCartao, altura, 2.5, 2.5, 'FD');

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8);
    doc.setTextColor(...COR.textoSecundario);
    doc.text(item.rotulo, x + 4, y + 6);

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(13);
    doc.setTextColor(...COR.texto);
    doc.text(item.valor, x + 4, y + 13.2);
  });

  return y + altura + 6;
}

/** Cartão com título e pares rótulo/valor em colunas (como as seções de detalhe das telas). Devolve o `y` abaixo do cartão. */
export function cartaoDeDados(
  doc: jsPDF, y: number, titulo: string, pares: [string, string][], colunas = 2, x = MARGEM, largura?: number
): number {
  const larg = largura ?? doc.internal.pageSize.getWidth() - 2 * MARGEM;
  const linhas = Math.ceil(pares.length / colunas);
  const altura = 13 + linhas * 11 + 2;

  doc.setFillColor(...COR.superficieBaixa);
  doc.setDrawColor(...COR.linha);
  doc.setLineWidth(0.25);
  doc.roundedRect(x, y, larg, altura, 2.5, 2.5, 'FD');

  doc.setFont('helvetica', 'bold');
  doc.setFontSize(10.5);
  doc.setTextColor(...COR.primaria);
  doc.text(titulo, x + 5, y + 8);

  const largColuna = (larg - 10) / colunas;
  pares.forEach(([rotulo, valor], i) => {
    const px = x + 5 + (i % colunas) * largColuna;
    const py = y + 15 + Math.floor(i / colunas) * 11;

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(7.5);
    doc.setTextColor(...COR.textoSecundario);
    doc.text(rotulo, px, py);

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(10);
    doc.setTextColor(...COR.texto);
    doc.text(doc.splitTextToSize(valor || '—', largColuna - 4)[0], px, py + 4.8);
  });

  return y + altura + 6;
}

/** Título de seção acima de uma tabela. */
export function tituloDeSecao(doc: jsPDF, y: number, texto: string): number {
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(10.5);
  doc.setTextColor(...COR.primaria);
  doc.text(texto, MARGEM, y);
  return y + 3;
}

/**
 * Estilo comum das tabelas: cabeçalho em superfície com texto secundário, linhas zebradas e separadas por fio fino,
 * rodapé (totais) no container da cor primária. `selo` é o índice da coluna de status, pintada como o selo das telas.
 */
export function estiloDeTabela(
  opcoes: { selo?: { coluna: number; tom: (valor: string) => TomSelo }; direita?: number[] } = {}
): Partial<UserOptions> {
  return {
    theme: 'plain',
    showFoot: 'lastPage',
    margin: { left: MARGEM, right: MARGEM, bottom: 18 },
    styles: {
      font: 'helvetica',
      fontSize: 9,
      textColor: COR.texto,
      cellPadding: { top: 2.8, bottom: 2.8, left: 3, right: 3 },
      lineWidth: 0
    },
    headStyles: { fillColor: COR.superficie, textColor: COR.textoSecundario, fontStyle: 'bold', fontSize: 8.5 },
    alternateRowStyles: { fillColor: COR.superficieBaixa },
    footStyles: { fillColor: COR.containerPrimaria, textColor: COR.sobreContainerPrimaria, fontStyle: 'bold', fontSize: 9.5 },
    didParseCell: (dados: CellHookData) => {
      // colunas de valor: cabeçalho, linhas e total alinhados à direita (o columnStyles só valeria para as linhas)
      if (opcoes.direita?.includes(dados.column.index)) dados.cell.styles.halign = 'right';
      const selo = opcoes.selo;
      if (selo && dados.section === 'body' && dados.column.index === selo.coluna) {
        const cores = SELO[selo.tom(String(dados.cell.raw))];
        dados.cell.styles.fillColor = cores.fundo;
        dados.cell.styles.textColor = cores.texto;
        dados.cell.styles.fontStyle = 'bold';
        dados.cell.styles.halign = 'center';
      }
    },
    didDrawCell: (dados: CellHookData) => {
      if (dados.section !== 'body') return;
      const { x, y, width, height } = dados.cell;
      dados.doc.setDrawColor(...COR.linha);
      dados.doc.setLineWidth(0.1);
      dados.doc.line(x, y + height, x + width, y + height);
    }
  };
}

/** "Locadora de Veículos" à esquerda e "Página x de y" à direita, em todas as páginas. */
export function rodapeDePaginas(doc: jsPDF): void {
  const total = doc.getNumberOfPages();
  const largura = doc.internal.pageSize.getWidth();
  const altura = doc.internal.pageSize.getHeight();

  for (let pagina = 1; pagina <= total; pagina++) {
    doc.setPage(pagina);
    doc.setDrawColor(...COR.linha);
    doc.setLineWidth(0.3);
    doc.line(MARGEM, altura - 13, largura - MARGEM, altura - 13);

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8);
    doc.setTextColor(...COR.textoSecundario);
    doc.text('Locadora de Veículos', MARGEM, altura - 8);
    doc.text(`Página ${pagina} de ${total}`, largura - MARGEM, altura - 8, { align: 'right' });
  }
}
