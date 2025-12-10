export interface TaxaServicoDto {
  id?: string;
  nome: string;
  preco: number;
  tipoCalculo: 'Fixo' | 'Diario';
}

export const TIPOS_CALCULO = ['Fixo', 'Diario'];
