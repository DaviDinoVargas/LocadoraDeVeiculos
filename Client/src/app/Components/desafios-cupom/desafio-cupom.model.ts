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
