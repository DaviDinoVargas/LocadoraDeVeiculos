import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NotificacaoService } from '../../core/notificacao.service';
import { criarNotificacaoFalsa } from '../../core/testing/notificacao-fake.spec';
import { of, throwError } from 'rxjs';
import { TaxasServicosListComponent } from './taxas-servicos-list.component';
import { TaxasServicosService } from './taxas-servicos.service';
import { TaxaServicoDto } from './taxa-servico.model';

describe('TaxasServicosListComponent — filtro por tipo no servidor', () => {
  let fixture: ComponentFixture<TaxasServicosListComponent>;
  let component: TaxasServicosListComponent;
  let svc: jasmine.SpyObj<TaxasServicosService>;

  const seguro = { id: 't1', nome: 'Seguro', preco: 30, tipoCalculo: 'Diario' } as TaxaServicoDto;
  const limpeza = { id: 't2', nome: 'Limpeza', preco: 50, tipoCalculo: 'Fixo' } as TaxaServicoDto;
  
  beforeEach(() => {
    svc = jasmine.createSpyObj('TaxasServicosService', ['listar', 'listarPorTipo']);
    svc.listar.and.returnValue(of([seguro, limpeza]));
    svc.listarPorTipo.and.returnValue(of([limpeza]));

    TestBed.configureTestingModule({
      imports: [TaxasServicosListComponent],
      providers: [provideRouter([]), { provide: TaxasServicosService, useValue: svc }, { provide: NotificacaoService, useValue: criarNotificacaoFalsa() }]
    });
    fixture = TestBed.createComponent(TaxasServicosListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

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
});
