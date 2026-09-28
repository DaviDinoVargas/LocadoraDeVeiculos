export interface AluguelDto {
  id?: string;
  condutorId: string;
  automovelId: string;
  clienteId: string;
  dataSaida: string;
  dataRetornoPrevisto: string;
  valorPrevisto: number;
  valorCaucao: number;
  status: 'Reservado' | 'EmAndamento' | 'Concluido' | 'Cancelado';
  taxasServicosIds: string[];
  cupomCodigo?: string | null;
}

export interface AluguelCompletoDto extends AluguelDto {
  condutorNome?: string;
  automovelPlaca?: string;
  clienteNome?: string;
  taxasServicos: TaxaServicoAluguelDto[];
  valorDesconto?: number;
}

export interface TaxaServicoAluguelDto {
  id: string;
  nome: string;
  preco: number;
  tipoCalculo: string;
}

export interface SelecionarAlugueisDto {
  id: string;
  condutorNome: string;
  automovelPlaca: string;
  clienteNome: string;
  dataSaida: string;
  dataRetornoPrevisto: string;
  valorPrevisto: number;
  status: string;
}
