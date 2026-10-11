import { menuParaCargo } from './navegacao';

describe('menuParaCargo — menu por papel', () => {
  const rotulos = (cargo: string) => menuParaCargo(cargo).flatMap(g => g.itens.map(i => i.rotulo));
  const item = (cargo: string, rotulo: string) =>
    menuParaCargo(cargo).flatMap(g => g.itens).find(i => i.rotulo === rotulo);

  it('Funcionario não vê itens só-Empresa (Funcionários, Parceiros, Cupons, Configurações)', () => {
    const itens = rotulos('Funcionario');

    expect(itens).toContain('Clientes');
    expect(itens).toContain('Taxas e serviços');
    for (const proibido of ['Funcionários', 'Parceiros', 'Cupons', 'Configurações']) {
      expect(itens).not.toContain(proibido);
    }
  });

  it('Funcionario vê Veículos mas sem os subitens Grupos e Planos', () => {
    expect(item('Funcionario', 'Veículos')?.filhos?.map(c => c.rotulo)).toEqual(['Todos os veículos']);
  });

  it('Empresa vê tudo, inclusive Grupos e Planos', () => {
    expect(rotulos('Empresa')).toContain('Funcionários');
    expect(item('Empresa', 'Veículos')?.filhos?.map(c => c.rotulo)).toEqual(['Todos os veículos', 'Grupos', 'Planos']);
  });

  it('cada módulo aparece UMA vez como grupo (antes o rótulo se repetia em todo item)', () => {
    const grupos = menuParaCargo('Empresa').map(g => g.rotulo);

    expect(new Set(grupos).size).toBe(grupos.length);
    expect(grupos).toEqual(['Visão geral', 'Gestão', 'Operacional', 'Financeiro', 'Configurações']);
  });

  it('grupos que ficam sem itens para o papel somem (Funcionario não tem "Configurações")', () => {
    expect(menuParaCargo('Funcionario').map(g => g.rotulo)).not.toContain('Configurações');
  });

  it('sem cargo (sem sessão) o menu é vazio', () => {
    expect(menuParaCargo(null)).toEqual([]);
  });
});
