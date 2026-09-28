export interface ParceiroDto {
  id?: string;
  nome: string;
  cnpj: string;
  categoria: string;
  ativo?: boolean;
}

export const CATEGORIAS_PARCEIRO = [
  { value: 'PostoDeCombustivel', label: 'Posto de Combustível' },
  { value: 'Hotel', label: 'Hotel' },
  { value: 'Seguradora', label: 'Seguradora' },
  { value: 'Restaurante', label: 'Restaurante' },
  { value: 'Oficina', label: 'Oficina' },
  { value: 'Outro', label: 'Outro' }
];

export function categoriaLabel(categoria: string): string {
  return CATEGORIAS_PARCEIRO.find(c => c.value === categoria)?.label ?? categoria;
}
