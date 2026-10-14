import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../auth/auth.service';
import { NotificacaoService } from '../../core/notificacao.service';
import { criarNotificacaoFalsa } from '../../core/testing/notificacao-fake.spec';
import { of, throwError } from 'rxjs';
import { TaxasServicosListComponent } from './taxas-servicos-list.component';
import { TaxasServicosService } from './taxas-servicos.service';
import { TaxaServicoDto } from './taxa-servico.model';
import { violacoesNosDoisTemas } from '../../core/testing/acessibilidade';

describe('TaxasServicosListComponent — filtro por tipo no servidor', () => {
  let fixture: ComponentFixture<TaxasServicosListComponent>;
  let component: TaxasServicosListComponent;
  let svc: jasmine.SpyObj<TaxasServicosService>;

  const seguro = { id: 't1', nome: 'Seguro', preco: 30, tipoCalculo: 'Diario' } as TaxaServicoDto;
  const limpeza = { id: 't2', nome: 'Limpeza', preco: 50, tipoCalculo: 'Fixo' } as TaxaServicoDto;
  
  const montar = (cargo: string) => {
    svc = jasmine.createSpyObj('TaxasServicosService', ['listar', 'listarPorTipo']);
    svc.listar.and.returnValue(of([seguro, limpeza]));
    svc.listarPorTipo.and.returnValue(of([limpeza]));

    TestBed.configureTestingModule({
      imports: [TaxasServicosListComponent],
      providers: [
        provideRouter([]),
        { provide: TaxasServicosService, useValue: svc },
        { provide: AuthService, useValue: { getUsuario: () => ({ cargo }) } },
        { provide: NotificacaoService, useValue: criarNotificacaoFalsa() }
      ]
    });
    fixture = TestBed.createComponent(TaxasServicosListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  };

  beforeEach(() => montar('Empresa'));

  it('escolher um tipo consulta GET /taxas-servicos/tipo/{tipo}', () => {
    component.filtrarPorTipo('Fixo');
    fixture.detectChanges();

    expect(svc.listarPorTipo).toHaveBeenCalledOnceWith('Fixo');
    expect(component.taxas()).toEqual([limpeza]);
    expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(1);
  });

  it('"Todos os tipos" volta a listar tudo, sem consultar o filtro por tipo', () => {
    component.filtrarPorTipo('Fixo');
    svc.listarPorTipo.calls.reset();

    component.filtrarPorTipo('');

    expect(svc.listarPorTipo).not.toHaveBeenCalled();
    expect(svc.listar).toHaveBeenCalledTimes(2);
    expect(component.taxas()).toEqual([seguro, limpeza]);
  });

  it('falha no filtro mostra o estado de erro e desliga o carregando', () => {
    svc.listarPorTipo.and.returnValue(throwError(() => new Error('falhou')));

    component.filtrarPorTipo('Diario');

    expect(component.erro()).toBeTrue();
    expect(component.carregando()).toBeFalse();
  });

  describe('permissão de escrita (S32): só a Empresa mexe em preço', () => {
    it('Empresa vê o botão "Nova taxa" e as ações de editar/excluir', () => {
      expect(component.podeEditar).toBeTrue();
      expect(component.acoes.map(a => a.rotulo)).toEqual(['Editar', 'Excluir']);
      expect(fixture.nativeElement.textContent).toContain('Nova taxa ou serviço');
    });

    it('Funcionário só consulta: sem botão de criar e sem ações por linha', () => {
      TestBed.resetTestingModule();
      montar('Funcionario');

      expect(component.podeEditar).toBeFalse();
      expect(component.acoes).toEqual([]);
      expect(fixture.nativeElement.textContent).not.toContain('Nova taxa ou serviço');
      expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(2);
    });
  });

  it('acessibilidade (axe-core, WCAG 2.2 AA): sem violações nos temas claro e escuro', async () => {
    TestBed.resetTestingModule();
    montar('Empresa');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(await violacoesNosDoisTemas(fixture.nativeElement)).toEqual([]);
  });
});
