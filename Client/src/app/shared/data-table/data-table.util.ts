/** Formata moeda em reais (Intl direto: o pipe `currency` usaria en-US porque o app não registra pt-BR). */
export function formatarMoeda(valor: unknown): string {
  const numero = typeof valor === 'number' ? valor : Number(valor);
  if (valor === null || valor === undefined || valor === '' || Number.isNaN(numero)) return '—';
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(numero);
}

export function formatarNumero(valor: unknown): string {
  const numero = typeof valor === 'number' ? valor : Number(valor);
  if (valor === null || valor === undefined || valor === '' || Number.isNaN(numero)) return '—';
  return new Intl.NumberFormat('pt-BR').format(numero);
}

const SO_DATA = /^(\d{4})-(\d{2})-(\d{2})$/;

/** dd/MM/yyyy. Datas "YYYY-MM-DD" não passam por `Date` (evita deslocar um dia por fuso). */
export function formatarData(valor: unknown): string {
  if (valor === null || valor === undefined || valor === '') return '—';
  if (typeof valor === 'string') {
    const m = SO_DATA.exec(valor.slice(0, 10));
    if (m && valor.length <= 10) return `${m[3]}/${m[2]}/${m[1]}`;
  }
  const data = valor instanceof Date ? valor : new Date(String(valor));
  if (Number.isNaN(data.getTime())) return '—';
  return new Intl.DateTimeFormat('pt-BR').format(data);
}

export function formatarDataHora(valor: unknown): string {
  if (valor === null || valor === undefined || valor === '') return '—';
  const data = valor instanceof Date ? valor : new Date(String(valor));
  if (Number.isNaN(data.getTime())) return '—';
  return new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(data);
}

/** Minúsculas e sem acentos, para busca tolerante ("jose" acha "José"). */
export function normalizar(texto: unknown): string {
  return String(texto ?? '')
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .trim();
}

const COLLATOR = new Intl.Collator('pt-BR', { numeric: true, sensitivity: 'base' });

/** Comparador de ordenação: números e datas pela grandeza, textos pelo alfabeto pt-BR; vazios no fim. */
export function compararValores(a: unknown, b: unknown): number {
  const vazioA = a === null || a === undefined || a === '';
  const vazioB = b === null || b === undefined || b === '';
  if (vazioA || vazioB) return vazioA === vazioB ? 0 : vazioA ? 1 : -1;
  if (typeof a === 'number' && typeof b === 'number') return a - b;
  if (typeof a === 'boolean' && typeof b === 'boolean') return Number(a) - Number(b);
  if (a instanceof Date && b instanceof Date) return a.getTime() - b.getTime();
  return COLLATOR.compare(String(a), String(b));
}

/** Escapa um campo para CSV (separador `;`, padrão do Excel em pt-BR). */
export function campoCsv(texto: string): string {
  return /[";\n\r]/.test(texto) ? `"${texto.replace(/"/g, '""')}"` : texto;
}

export function baixarCsv(nomeArquivo: string, linhas: string[][]): void {
  const conteudo = '﻿' + linhas.map(l => l.map(campoCsv).join(';')).join('\r\n');
  const blob = new Blob([conteudo], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = nomeArquivo.endsWith('.csv') ? nomeArquivo : `${nomeArquivo}.csv`;
  a.click();
  URL.revokeObjectURL(url);
}
