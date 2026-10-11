import { Route } from '@angular/router';
import { routes } from './app.routes';
import { roleGuard } from './core/role.guard';
import { EMPRESA_E_FUNCIONARIO, SOMENTE_EMPRESA } from './core/permissoes';

const protegidas = routes.filter(r => r.canActivate && r.path !== undefined);
const papeisDe = (path: string) => (routes.find(r => r.path === path) as Route).data?.['roles'];

describe('app.routes — papéis por rota', () => {
  it('toda rota protegida pelo AuthGuard também declara os papéis e usa o roleGuard', () => {
    expect(protegidas.length).toBeGreaterThan(30);
    for (const r of protegidas) {
      expect(r.canActivate).withContext(`rota "${r.path}"`).toContain(roleGuard);
      expect(r.data?.['roles']).withContext(`rota "${r.path}"`).toBeDefined();
    }
  });

  it('rotas de gestão (API só-Empresa) não ficam abertas ao Funcionario', () => {
    for (const path of ['funcionarios', 'planos-cobranca', 'grupoveiculos', 'parceiros',
      'cupons', 'desafios-cupom', 'configuracoes/combustivel', 'veiculos/new', 'veiculos/:id/edit']) {
      expect(papeisDe(path)).withContext(path).toEqual(SOMENTE_EMPRESA);
    }
  });

  it('rotas operacionais continuam liberadas aos dois papéis', () => {
    for (const path of ['home', 'veiculos', 'clientes', 'condutores', 'alugueis',
      'devolucoes', 'taxas-servicos', 'camera-monitor', 'verificacao-facial']) {
      expect(papeisDe(path)).withContext(path).toEqual(EMPRESA_E_FUNCIONARIO);
    }
  });
});
