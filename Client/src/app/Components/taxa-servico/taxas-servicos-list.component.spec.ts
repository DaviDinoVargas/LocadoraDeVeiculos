import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';
import { TaxasServicosListComponent } from './taxas-servicos-list.component';
import { TaxasServicosService } from './taxas-servicos.service';
import { TaxaServicoDto } from './taxa-servico.model';

describe('TaxasServicosListComponent — filtro por tipo no servidor', () => {
  let fixture: ComponentFixture<TaxasServicosListComponent>;
  let component: TaxasServicosListComponent;
  let svc: jasmine.SpyObj<TaxasServicosService>;
  let snack: { open: jasmine.Spy };

  const seguro = { id: 't1', nome: 'Seguro', preco: 30, tipoCalculo: 'Diario' } as TaxaServicoDto;
  const limpeza = { id: 't2', nome: 'Limpeza', preco: 50, tipoCalculo: 'Fixo' } as TaxaServicoDto;
  const evento = (valor: string) => ({ target: { value: valor } }) as unknown as Event;

  beforeEach(() => {
    svc = jasmine.createSpyObj('TaxasServicosService', ['listar', 'listarPorTipo']);
    svc.listar.and.returnValue(of([seguro, limpeza]));
    svc.listarPorTipo.and.returnValue(of([limpeza]));
    snack = { open: jasmine.createSpy('open') };

    TestBed.configureTestingModule({
      imports: [TaxasServicosListComponent],
      providers: [provideRouter([]), { provide: TaxasServicosService, useValue: svc }, { provide: MatSnackBar, useValue: snack }]
    });
    fixture = TestBed.createComponent(TaxasServicosListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('escolher um tipo consulta GET /taxas-servicos/tipo/{tipo}', () => {
    component.filtrarPorTipo(evento('Fixo'));
    fixture.detectChanges();

    expect(svc.listarPorTipo).toHaveBeenCalledOnceWith('Fixo');
    expect(component.taxasFiltradas).toEqual([limpeza]);
    expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(1);
  });

  it('"Todos os tipos" reaproveita a lista já carregada, sem nova chamada', () => {
    component.filtrarPorTipo(evento('Fixo'));
    svc.listarPorTipo.calls.reset();

    component.filtrarPorTipo(evento(''));

    expect(svc.listarPorTipo).not.toHaveBeenCalled();
    expect(component.taxasFiltradas).toEqual([seguro, limpeza]);
  });

  it('falha no filtro avisa o usuário e desliga o carregando', () => {
    svc.listarPorTipo.and.returnValue(throwError(() => new Error('falhou')));
    spyOn(console, 'error');

    component.filtrarPorTipo(evento('Diario'));

    expect(snack.open).toHaveBeenCalled();
    expect(component.loading).toBeFalse();
  });
});
