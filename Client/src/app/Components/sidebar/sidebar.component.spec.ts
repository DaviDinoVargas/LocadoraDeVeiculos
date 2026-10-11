import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../auth/auth.service';
import { SidebarComponent } from './sidebar.component';

describe('SidebarComponent — menu por papel', () => {
  const montar = (cargo: string): SidebarComponent => {
    TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: { getUsuario: () => ({ cargo }) } }]
    });
    const fixture: ComponentFixture<SidebarComponent> = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();
    return fixture.componentInstance;
  };
  const rotulos = (c: SidebarComponent) => c.menuItems.map(i => i.label);

  it('Funcionario não vê itens só-Empresa (Funcionários, Parceiros, Cupons, Configurações)', () => {
    const itens = rotulos(montar('Funcionario'));

    expect(itens).toContain('Clientes');
    expect(itens).toContain('Taxas e Serviços');
    for (const proibido of ['Funcionários', 'Parceiros', 'Cupons', 'Configurações']) {
      expect(itens).not.toContain(proibido);
    }
  });

  it('Funcionario vê Veículos mas sem os subitens Grupos e Planos', () => {
    const veiculos = montar('Funcionario').menuItems.find(i => i.label === 'Veículos');

    expect(veiculos?.children?.map(c => c.label)).toEqual(['Todos Veículos']);
  });

  it('Empresa vê tudo, inclusive Grupos e Planos', () => {
    const c = montar('Empresa');
    const veiculos = c.menuItems.find(i => i.label === 'Veículos');

    expect(rotulos(c)).toContain('Funcionários');
    expect(veiculos?.children?.map(x => x.label)).toEqual(['Todos Veículos', 'Grupos', 'Planos']);
  });
});
