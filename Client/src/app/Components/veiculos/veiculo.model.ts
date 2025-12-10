export interface VeiculoDto {
  id?: string;
  placa: string;
  marca: string;
  cor: string;
  modelo: string;
  tipoCombustivel: string;
  capacidadeTanque: number;
  ano: number;
  foto?: string;
  grupoAutomovelId: string;
}

export interface VeiculoComGrupoDto extends VeiculoDto {
  grupoAutomovelNome?: string;
}
