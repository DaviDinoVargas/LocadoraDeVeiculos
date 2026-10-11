import { TemplateRef } from '@angular/core';
import { TomStatus } from '../layout/status-badge.component';

export type TipoColuna = 'texto' | 'numero' | 'moeda' | 'data' | 'enum' | 'booleano' | 'custom';

export interface OpcaoColuna {
  valor: string;
  rotulo: string;
  /** Tom do selo quando a coluna é do tipo enum (selo = ícone + texto). */
  tom?: TomStatus;
}

export interface ColunaTabela<T> {
  /** Id estável; também é o nome do parâmetro de filtro na URL (`?status=Ativo`). */
  id: string;
  cabecalho: string;
  tipo?: TipoColuna;
  /** Valor bruto usado em busca, ordenação, filtro e exportação. Padrão: `linha[id]`. */
  valor?: (linha: T) => unknown;
  /** Texto mostrado. Padrão: formatação pelo tipo (moeda/data pt-BR, "Sim"/"Não", "—" para vazio). */
  formatar?: (linha: T) => string;
  /** Opções de enum (rótulo/tom). Para `filtravel` sem opções, saem dos valores presentes nos dados. */
  opcoes?: OpcaoColuna[];
  ordenavel?: boolean;
  filtravel?: boolean;
  alinhar?: 'inicio' | 'fim' | 'centro';
  /** Começa oculta (o usuário pode mostrar no menu de colunas). */
  ocultaPorPadrao?: boolean;
  /** `baixa` some em telas menores que 1536 px (continua no menu de colunas e na exportação). */
  prioridade?: 'alta' | 'baixa';
  /** Largura máxima do texto antes de truncar com reticências (em `ch`). */
  truncar?: number;
}

export interface AcaoLinha<T> {
  rotulo: string;
  icone: string;
  executar: (linha: T) => void;
  destrutiva?: boolean;
  visivel?: (linha: T) => boolean;
}

/** Contexto entregue ao template de célula customizada (`<ng-template appCelula="id" let-linha>`). */
export interface ContextoCelula<T> {
  $implicit: T;
}

export type TemplateCelula<T> = TemplateRef<ContextoCelula<T>>;

export const TAMANHOS_PAGINA = [10, 25, 50, 100] as const;
export type Densidade = 'compacta' | 'padrao' | 'confortavel';
