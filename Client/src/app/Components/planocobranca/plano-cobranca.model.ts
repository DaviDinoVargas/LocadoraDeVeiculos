export interface PlanoCobrancaDto {
  id?: string;
  grupoAutomovelId: string;
  nome: string;
  precoDiaria: number;
  precoPorKm?: number;
  kmLivreLimite?: number;
}

export interface PlanoCobrancaComGrupoDto extends PlanoCobrancaDto {
  grupoAutomovelNome?: string;
}
