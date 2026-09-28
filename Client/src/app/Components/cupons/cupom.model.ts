export interface CupomDto {
  id?: string;
  codigo: string;
  descricao: string;
  tipoDesconto: string;
  valorDesconto: number;
  validoAte: string;
  limiteUsos: number;
  usosAtuais?: number;
  parceiroId?: string | null;
  parceiroNome?: string;
  ativo?: boolean;
}

export const TIPOS_DESCONTO = [
  { value: 'Percentual', label: 'Percentual (%)' },
  { value: 'ValorFixo', label: 'Valor fixo (R$)' }
];

export function tipoDescontoLabel(tipo: string): string {
  return TIPOS_DESCONTO.find(t => t.value === tipo)?.label ?? tipo;
}
