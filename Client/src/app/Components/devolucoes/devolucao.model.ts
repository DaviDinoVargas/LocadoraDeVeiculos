export interface DevolucaoDto {
  id?: string;
  aluguelId: string;
  dataDevolucao: string;
  quilometragemFinal: number;
  combustivelNoTanque: number;
  nivelCombustivel: 'Cheio' | 'TresQuartos' | 'Metade' | 'UmQuarto' | 'Vazio';
  valorMultas: number;
  valorAdicionalCombustivel: number;
  valorTotal: number;
}

export interface DevolucaoCompletoDto extends DevolucaoDto {
  condutorNome?: string;
  automovelPlaca?: string;
  clienteNome?: string;
  aluguel?: any;
}

export interface SelecionarDevolucoesDto {
  id: string;
  condutorNome: string;
  automovelPlaca: string;
  clienteNome: string;
  dataDevolucao: string;
  quilometragemFinal: number;
  valorTotal: number;
}

export const NIVEL_COMBUSTIVEL = {
  Cheio: 'Cheio',
  TresQuartos: 'Três Quartos',
  Metade: 'Metade',
  UmQuarto: 'Um Quarto',
  Vazio: 'Vazio'
};

export const NIVEL_COMBUSTIVEL_OPTIONS = [
  { value: 'Cheio', label: 'Cheio' },
  { value: 'TresQuartos', label: 'Três Quartos' },
  { value: 'Metade', label: 'Metade' },
  { value: 'UmQuarto', label: 'Um Quarto' },
  { value: 'Vazio', label: 'Vazio' }
];
