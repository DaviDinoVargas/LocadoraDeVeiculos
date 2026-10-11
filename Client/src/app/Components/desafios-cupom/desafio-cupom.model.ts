// Item de GET /api/desafios-cupom/cliente/{id}/progresso
export interface ProgressoDesafioDto {
  desafioId: string;
  nome: string;
  meta: number;
  progresso: number;
  cumprido: boolean;
  cupomLiberadoCodigo: string | null;
}

export interface DesafioCupomDto {
  id?: string;
  nome: string;
  descricao: string;
  metaQuantidadeAlugueis: number;
  periodoDias: number;
  cupomRecompensaId?: string | null;
  cupomRecompensaCodigo?: string;
  ativo?: boolean;
}
