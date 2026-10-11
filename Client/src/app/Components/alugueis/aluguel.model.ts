export interface AluguelDto {
  id?: string;
  condutorId: string;
  automovelId: string;
  clienteId: string;
  dataSaida: string;
  dataRetornoPrevisto: string;
  valorPrevisto: number;
  valorCaucao?: number; // definido pelo servidor (Configuracao.ValorGarantia)
  status: 'Reservado' | 'EmAndamento' | 'Concluido' | 'Cancelado';
  taxasServicosIds: string[];
  cupomCodigo?: string | null;
}

// Resposta de POST /api/alugueis/simular: o mesmo cálculo que o servidor grava ao salvar.
export interface SimulacaoAluguelDto {
  dias: number;
  planoNome: string;
  precoDiaria: number;
  valorDiarias: number;
  valorTaxas: number;
  valorPrevisto: number; // diárias + taxas, ANTES de qualquer cupom
  valorCaucao: number;
}

export interface SimularAluguelRequest {
  automovelId: string;
  dataSaida: string;
  dataRetornoPrevisto: string;
  taxasServicosIds: string[];
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
