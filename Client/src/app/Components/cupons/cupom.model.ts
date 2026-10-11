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

// Resposta de POST /api/cupons/validar: confere se o código pode ser usado agora e quanto abate.
export interface ValidacaoCupomDto {
  valido: boolean;
  motivoInvalido: string | null;
  valorDesconto: number;
  valorComDesconto: number;
}

export const TIPOS_DESCONTO = [
  { value: 'Percentual', label: 'Percentual (%)' },
  { value: 'ValorFixo', label: 'Valor fixo (R$)' }
];

export function tipoDescontoLabel(tipo: string): string {
  return TIPOS_DESCONTO.find(t => t.value === tipo)?.label ?? tipo;
}
