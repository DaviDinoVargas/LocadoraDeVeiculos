import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { AluguelFormComponent } from './aluguel-form.component';
import { AlugueisService } from './alugueis.service';
import { ClientesService } from '../clientes/clientes.service';
import { CondutoresService } from '../condutor/condutores.service';
import { VeiculosService } from '../veiculos/veiculos.service';
import { TaxasServicosService } from '../taxa-servico/taxas-servicos.service';
import { AluguelDto, SimulacaoAluguelDto } from './aluguel.model';
import { VeiculoDto } from '../veiculos/veiculo.model';

describe('AluguelFormComponent — resumo de valor vindo do servidor', () => {
  let fixture: ComponentFixture<AluguelFormComponent>;
  let component: AluguelFormComponent;
  let alugueis: jasmine.SpyObj<AlugueisService>;
  let veiculos: jasmine.SpyObj<VeiculosService>;

  const fusca = { id: 'v1', placa: 'AAA1A11', marca: 'VW', modelo: 'Fusca' } as VeiculoDto;
  const uno = { id: 'v2', placa: 'BBB2B22', marca: 'Fiat', modelo: 'Uno' } as VeiculoDto;

  const simulacao: SimulacaoAluguelDto = {
    dias: 3, planoNome: 'Plano Padrão', precoDiaria: 100, valorDiarias: 300,
    valorTaxas: 90, valorPrevisto: 390, valorCaucao: 1000
  };

  beforeEach(async () => {
    alugueis = jasmine.createSpyObj('AlugueisService', ['simular', 'criar', 'atualizar', 'obter']);
    alugueis.simular.and.returnValue(of(simulacao));
    alugueis.criar.and.returnValue(of({} as unknown as AluguelDto));
    veiculos = jasmine.createSpyObj('VeiculosService', ['listar', 'listarDisponiveis']);
    veiculos.listar.and.returnValue(of([fusca, uno]));
    veiculos.listarDisponiveis.and.returnValue(of([uno]));

    await TestBed.configureTestingModule({
      imports: [AluguelFormComponent],
      providers: [
        provideRouter([]),
        { provide: AlugueisService, useValue: alugueis },
        { provide: ClientesService, useValue: { listar: () => of([]) } },
        { provide: CondutoresService, useValue: { listar: () => of([]) } },
        { provide: VeiculosService, useValue: veiculos },
        { provide: TaxasServicosService, useValue: { listar: () => of([{ id: 't1', nome: 'Seguro', preco: 30, tipoCalculo: 'Diario' }]) } },
        { provide: MatSnackBar, useValue: { open: () => {} } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AluguelFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('não tem mais o campo "Valor Previsto" digitável (o servidor ignorava esse valor)', () => {
    expect(component.form.contains('valorPrevisto')).toBeFalse();
    expect(fixture.nativeElement.querySelector('input[formControlName="valorPrevisto"]')).toBeNull();
  });

  it('simula quando veículo e datas válidos são informados, e só depois do debounce', fakeAsync(() => {
    component.form.patchValue({ automovelId: 'v2', dataSaida: '2030-01-10', dataRetornoPrevisto: '2030-01-13' });
    tick(100);
    expect(alugueis.simular).not.toHaveBeenCalled();

    tick(250);
    expect(alugueis.simular).toHaveBeenCalledOnceWith({
      automovelId: 'v2', dataSaida: '2030-01-10', dataRetornoPrevisto: '2030-01-13', taxasServicosIds: []
    });
    expect(component.simulacao).toEqual(simulacao);
  }));

  it('não simula com retorno antes da saída ou sem veículo', fakeAsync(() => {
    component.form.patchValue({ dataSaida: '2030-01-10', dataRetornoPrevisto: '2030-01-09' });
    tick(400);
    component.form.patchValue({ automovelId: 'v2' });
    tick(400);

    expect(alugueis.simular).not.toHaveBeenCalled();
    expect(component.simulacao).toBeNull();
  }));

  it('inclui as taxas marcadas no pedido de simulação', fakeAsync(() => {
    component.form.patchValue({ automovelId: 'v2', dataSaida: '2030-01-10', dataRetornoPrevisto: '2030-01-13' });
    tick(400);
    component.onTaxaChange({ target: { checked: true } }, { id: 't1', nome: 'Seguro', preco: 30, tipoCalculo: 'Diario' } as never);
    tick(400);

    expect(alugueis.simular.calls.mostRecent().args[0].taxasServicosIds).toEqual(['t1']);
  }));

  it('total a pagar = diárias + taxas + caução (sem cupom)', fakeAsync(() => {
    component.form.patchValue({ automovelId: 'v2', dataSaida: '2030-01-10', dataRetornoPrevisto: '2030-01-13' });
    tick(400);

    expect(component.totalAPagar()).toBe(1390);
  }));

  it('mostra no painel o erro do servidor (ex.: sem plano para o grupo) em vez de abrir snackbar', fakeAsync(() => {
    alugueis.simular.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 400, error: ['Não há plano de cobrança cadastrado para o grupo deste automóvel.']
    })));

    component.form.patchValue({ automovelId: 'v2', dataSaida: '2030-01-10', dataRetornoPrevisto: '2030-01-13' });
    tick(400);
    fixture.detectChanges();

    expect(component.erroSimulacao).toContain('plano de cobrança');
    expect(component.simulacao).toBeNull();
    expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent).toContain('plano de cobrança');
  }));

  it('mostra a mensagem de negócio do servidor ao falhar ao salvar (409 de disponibilidade)', () => {
    alugueis.criar.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 409, error: ['O automóvel já está reservado para este período.']
    })));
    component.form.patchValue({
      clienteId: 'c1', condutorId: 'd1', automovelId: 'v2', dataSaida: '2030-01-10', dataRetornoPrevisto: '2030-01-13'
    });

    component.salvar();

    expect(component.serverErrors).toEqual(['O automóvel já está reservado para este período.']);
  });
  describe('disponibilidade de veículos no período', () => {
    const periodo = { dataSaida: '2030-01-10', dataRetornoPrevisto: '2030-01-13' };

    it('antes das datas, oferece todos os veículos (não dá para saber quem está livre)', () => {
      expect(component.veiculosDisponiveis).toEqual([fusca, uno]);
      expect(veiculos.listarDisponiveis).not.toHaveBeenCalled();
    });

    it('com o período escolhido, consulta o servidor e oferece só os veículos livres', fakeAsync(() => {
      component.form.patchValue(periodo);
      tick(400);

      expect(veiculos.listarDisponiveis).toHaveBeenCalledOnceWith('2030-01-10', '2030-01-13', undefined);
      expect(component.veiculosDisponiveis).toEqual([uno]);
    }));

    it('limpa o veículo já escolhido quando ele deixa de estar disponível e avisa', fakeAsync(() => {
      component.form.patchValue({ automovelId: 'v1' });
      component.form.patchValue(periodo);
      tick(400);

      expect(component.form.get('automovelId')?.value).toBe('');
      expect(component.avisoDisponibilidade).toContain('não está disponível');
    }));

    it('mantém o veículo escolhido quando ele continua disponível', fakeAsync(() => {
      component.form.patchValue({ automovelId: 'v2' });
      component.form.patchValue(periodo);
      tick(400);

      expect(component.form.get('automovelId')?.value).toBe('v2');
      expect(component.avisoDisponibilidade).toBe('');
    }));

    it('avisa quando nenhum veículo está livre', fakeAsync(() => {
      veiculos.listarDisponiveis.and.returnValue(of([]));
      component.form.patchValue(periodo);
      tick(400);

      expect(component.avisoDisponibilidade).toBe('Nenhum veículo disponível neste período.');
    }));

    it('se a consulta falhar, volta à lista completa (o servidor ainda valida ao salvar)', fakeAsync(() => {
      veiculos.listarDisponiveis.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
      component.form.patchValue(periodo);
      tick(400);

      expect(component.veiculosDisponiveis).toEqual([fusca, uno]);
    }));

    it('não consulta com retorno antes da saída', fakeAsync(() => {
      component.form.patchValue({ dataSaida: '2030-01-13', dataRetornoPrevisto: '2030-01-10' });
      tick(400);

      expect(veiculos.listarDisponiveis).not.toHaveBeenCalled();
    }));

    it('na edição envia o id do aluguel para ele não bloquear o próprio veículo', fakeAsync(() => {
      component.id = 'aluguel-1';
      component.form.patchValue(periodo);
      tick(400);

      expect(veiculos.listarDisponiveis).toHaveBeenCalledWith('2030-01-10', '2030-01-13', 'aluguel-1');
    }));
  });
  describe('edição de aluguel', () => {
    it('mostra as datas no formato do input date (a API devolve ISO e o campo ficava em branco)', () => {
      alugueis.obter.and.returnValue(of({
        id: 'a1', condutorId: 'd1', clienteId: 'c1', automovelId: 'v2',
        dataSaida: '2030-01-10T00:00:00+00:00', dataRetornoPrevisto: '2030-01-13T00:00:00+00:00',
        valorPrevisto: 390, valorCaucao: 1000, status: 'Reservado', taxasServicosIds: [], taxasServicos: [], valorDesconto: 0
      }));

      component.carregar('a1');

      expect(component.form.get('dataSaida')?.value).toBe('2030-01-10');
      expect(component.form.get('dataRetornoPrevisto')?.value).toBe('2030-01-13');
    });
  });
});
