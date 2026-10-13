import { EMPRESA_E_FUNCIONARIO, SOMENTE_EMPRESA, cargoPodeAcessar } from './permissoes';

export interface ItemMenu {
  rotulo: string;
  icone: string;
  caminho: string;
  roles: readonly string[];
  filhos?: ItemMenu[];
}

export interface GrupoMenu {
  rotulo: string;
  itens: ItemMenu[];
}

/**
 * Menu do app. Cada módulo (Gestão, Operacional...) aparece UMA vez como rótulo de grupo, com os
 * itens embaixo (antes o rótulo se repetia em todo item). Os papéis vêm de core/permissoes.ts.
 */
export const MENU: readonly GrupoMenu[] = [
  {
    rotulo: 'Visão geral',
    itens: [{ rotulo: 'Home', icone: 'lucideHouse', caminho: '/home', roles: EMPRESA_E_FUNCIONARIO }]
  },
  {
    rotulo: 'Gestão',
    itens: [
      { rotulo: 'Funcionários', icone: 'lucideUserCog', caminho: '/funcionarios', roles: SOMENTE_EMPRESA },
      {
        rotulo: 'Clientes',
        icone: 'lucideUsers',
        caminho: '/clientes',
        roles: EMPRESA_E_FUNCIONARIO,
        filhos: [
          { rotulo: 'Todos os clientes', icone: 'lucideUsers', caminho: '/clientes', roles: EMPRESA_E_FUNCIONARIO },
          { rotulo: 'Pessoas físicas', icone: 'lucideUser', caminho: '/clientes/pf', roles: EMPRESA_E_FUNCIONARIO },
          { rotulo: 'Pessoas jurídicas', icone: 'lucideBuilding2', caminho: '/clientes/pj', roles: EMPRESA_E_FUNCIONARIO }
        ]
      },
      { rotulo: 'Condutores', icone: 'lucideIdCard', caminho: '/condutores', roles: EMPRESA_E_FUNCIONARIO },
      {
        rotulo: 'Veículos',
        icone: 'lucideCar',
        caminho: '/veiculos',
        roles: EMPRESA_E_FUNCIONARIO,
        filhos: [
          { rotulo: 'Todos os veículos', icone: 'lucideCar', caminho: '/veiculos', roles: EMPRESA_E_FUNCIONARIO },
          { rotulo: 'Grupos', icone: 'lucideLayers', caminho: '/grupoveiculos', roles: SOMENTE_EMPRESA },
          { rotulo: 'Planos', icone: 'lucideCoins', caminho: '/planos-cobranca', roles: SOMENTE_EMPRESA }
        ]
      }
    ]
  },
  {
    rotulo: 'Operacional',
    itens: [
      {
        rotulo: 'Aluguéis',
        icone: 'lucideReceipt',
        caminho: '/alugueis',
        roles: EMPRESA_E_FUNCIONARIO,
        filhos: [
          { rotulo: 'Novo aluguel', icone: 'lucidePlus', caminho: '/alugueis/new', roles: EMPRESA_E_FUNCIONARIO },
          { rotulo: 'Em aberto', icone: 'lucideClock', caminho: '/alugueis?status=em-aberto', roles: EMPRESA_E_FUNCIONARIO },
          { rotulo: 'Todos', icone: 'lucideReceipt', caminho: '/alugueis', roles: EMPRESA_E_FUNCIONARIO }
        ]
      },
      { rotulo: 'Devoluções', icone: 'lucideUndo2', caminho: '/devolucoes', roles: EMPRESA_E_FUNCIONARIO },
      { rotulo: 'Monitoramento', icone: 'lucideVideo', caminho: '/camera-monitor', roles: EMPRESA_E_FUNCIONARIO },
      { rotulo: 'Verificação facial', icone: 'lucideScanFace', caminho: '/verificacao-facial', roles: EMPRESA_E_FUNCIONARIO }
    ]
  },
  {
    rotulo: 'Financeiro',
    itens: [
      { rotulo: 'Taxas e serviços', icone: 'lucideCoins', caminho: '/taxas-servicos', roles: EMPRESA_E_FUNCIONARIO },
      { rotulo: 'Parceiros', icone: 'lucideBriefcase', caminho: '/parceiros', roles: SOMENTE_EMPRESA },
      {
        rotulo: 'Cupons',
        icone: 'lucideTag',
        caminho: '/cupons',
        roles: SOMENTE_EMPRESA,
        filhos: [
          { rotulo: 'Todos os cupons', icone: 'lucideTag', caminho: '/cupons', roles: SOMENTE_EMPRESA },
          { rotulo: 'Desafios de cupom', icone: 'lucideTrophy', caminho: '/desafios-cupom', roles: SOMENTE_EMPRESA }
        ]
      }
    ]
  },
  {
    rotulo: 'Configurações',
    itens: [
      { rotulo: 'Auditoria', icone: 'lucideClipboardList', caminho: '/auditoria', roles: SOMENTE_EMPRESA },
      {
        rotulo: 'Configurações',
        icone: 'lucideSettings',
        caminho: '/configuracoes',
        roles: SOMENTE_EMPRESA,
        filhos: [
          { rotulo: 'Preço do combustível', icone: 'lucideFuel', caminho: '/configuracoes/combustivel', roles: SOMENTE_EMPRESA }
        ]
      }
    ]
  }
];

/** Menu filtrado pelo papel do usuário: itens, subitens e grupos que sobrarem vazios saem. */
export function menuParaCargo(cargo: string | null | undefined): GrupoMenu[] {
  return MENU.map(grupo => ({
    rotulo: grupo.rotulo,
    itens: grupo.itens
      .filter(item => cargoPodeAcessar(cargo, item.roles))
      .map(item =>
        item.filhos ? { ...item, filhos: item.filhos.filter(filho => cargoPodeAcessar(cargo, filho.roles)) } : item
      )
      .filter(item => !item.filhos || item.filhos.length > 0)
  })).filter(grupo => grupo.itens.length > 0);
}
