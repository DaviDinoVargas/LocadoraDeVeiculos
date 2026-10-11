import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
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
import { CondutorDto } from '../condutor/condutor.model';
import { CuponsService } from '../cupons/cupons.service';
import { DesafiosCupomService } from '../desafios-cupom/desafios-cupom.service';
import { ProgressoDesafioDto } from '../desafios-cupom/desafio-cupom.model';

describe('AluguelFormComponent — resumo de valor vindo do servidor', () => {
  let fixture: ComponentFixture<AluguelFormComponent>;
  let component: AluguelFormComponent;
  let alugueis: jasmine.SpyObj<AlugueisService>;
  let veiculos: jasmine.SpyObj<VeiculosService>;
  let cupons: jasmine.SpyObj<CuponsService>;
  let condutores: jasmine.SpyObj<CondutoresService>;
  let desafios: jasmine.SpyObj<DesafiosCupomService>;

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
    condutores = jasmine.createSpyObj('CondutoresService', ['listarPorCliente']);
    condutores.listarPorCliente.and.returnValue(of([{ id: 'd1', nome: 'Condutor 1', cpf: '1', clienteId: 'c1' } as CondutorDto]));
    cupons = jasmine.createSpyObj('CuponsService', ['validar']);
    cupons.validar.and.returnValue(of({ valido: true, motivoInvalido: null, valorDesconto: 39, valorComDesconto: 351 }));
    desafios = jasmine.createSpyObj('DesafiosCupomService', ['progressoDoCliente']);
    desafios.progressoDoCliente.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [AluguelFormComponent],
      providers: [
        provideRouter([]),
        { provide: AlugueisService, useValue: alugueis },
        { provide: ClientesService, useValue: { listar: () => of([]) } },
        { provide: CondutoresService, useValue: condutores },
        { provide: VeiculosService, useValue: veiculos },
        { provide: CuponsService, useValue: cupons },
        { provide: DesafiosCupomService, useValue: desafios },
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
  describe('cupom de desconto (validado no servidor)', () => {
    const preencherEBuscarSimulacao = () => {
      component.form.patchValue({ automovelId: 'v2', dataSaida: '2030-01-10', dataRetornoPrevisto: '2030-01-13' });
      tick(400);
    };

    it('sem código: nada a validar e sem desconto', fakeAsync(() => {
      preencherEBuscarSimulacao();
      component.form.patchValue({ cupomCodigo: '' });
      tick(500);

      expect(component.cupomStatus).toBe('vazio');
      expect(cupons.validar).not.toHaveBeenCalled();
    }));

    it('antes de haver simulação pede veículo e datas em vez de validar', fakeAsync(() => {
      component.form.patchValue({ cupomCodigo: 'PROMO10' });
      tick(500);

      expect(component.cupomStatus).toBe('aguardando');
      expect(cupons.validar).not.toHaveBeenCalled();
    }));

    it('cupom válido: usa o valor simulado como base, mostra o desconto e o total o desconta', fakeAsync(() => {
      preencherEBuscarSimulacao();
      component.form.patchValue({ cupomCodigo: 'promo10 ' }); // minúsculo e com espaço
      tick(500);

      expect(cupons.validar).toHaveBeenCalledOnceWith('PROMO10', 390);
      expect(component.cupomStatus).toBe('valido');
      expect(component.descontoCupom).toBe(39);
      expect(component.totalAPagar()).toBe(390 - 39 + 1000);
    }));

    it('cupom inválido: mostra o motivo do servidor, zera o desconto e bloqueia o salvar', fakeAsync(() => {
      cupons.validar.and.returnValue(of({ valido: false, motivoInvalido: 'Cupom expirado.', valorDesconto: 0, valorComDesconto: 390 }));
      preencherEBuscarSimulacao();
      component.form.patchValue({ clienteId: 'c1', condutorId: 'd1', cupomCodigo: 'VELHO' });
      tick(500);

      expect(component.cupomStatus).toBe('invalido');
      expect(component.cupomMensagem).toBe('Cupom expirado.');
      expect(component.descontoCupom).toBe(0);

      component.salvar();
      expect(alugueis.criar).not.toHaveBeenCalled();
    }));

    it('falha na validação vira "inválido" com a mensagem do servidor, sem derrubar o formulário', fakeAsync(() => {
      cupons.validar.and.returnValue(throwError(() => new HttpErrorResponse({ status: 400, error: ['Cupom não encontrado.'] })));
      preencherEBuscarSimulacao();
      component.form.patchValue({ cupomCodigo: 'XYZ' });
      tick(500);

      expect(component.cupomStatus).toBe('invalido');
      expect(component.cupomMensagem).toBe('Cupom não encontrado.');
    }));

    it('revalida o cupom quando o valor muda (desconto percentual depende da base)', fakeAsync(() => {
      preencherEBuscarSimulacao();
      component.form.patchValue({ cupomCodigo: 'PROMO10' });
      tick(500);
      cupons.validar.calls.reset();

      alugueis.simular.and.returnValue(of({ ...simulacao, valorTaxas: 0, valorPrevisto: 300 }));
      component.form.patchValue({ dataRetornoPrevisto: '2030-01-14' });
      tick(1000);

      expect(cupons.validar).toHaveBeenCalledWith('PROMO10', 300);
    }));

    it('envia o cupom ao salvar quando válido', fakeAsync(() => {
      spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
      preencherEBuscarSimulacao();
      component.form.patchValue({ clienteId: 'c1', condutorId: 'd1', cupomCodigo: 'promo10' });
      tick(500);

      component.salvar();

      expect(alugueis.criar.calls.mostRecent().args[0].cupomCodigo).toBe('promo10');
    }));
  });

  describe('desafios do cliente', () => {
    const progresso: ProgressoDesafioDto[] = [
      { desafioId: 'x1', nome: 'Cliente fiel', meta: 5, progresso: 5, cumprido: true, cupomLiberadoCodigo: 'FIEL10' },
      { desafioId: 'x2', nome: 'Viajante', meta: 10, progresso: 3, cumprido: false, cupomLiberadoCodigo: null }
    ];

    it('ao escolher o cliente, carrega o progresso dos desafios', () => {
      desafios.progressoDoCliente.and.returnValue(of(progresso));

      component.form.patchValue({ clienteId: 'c1' });

      expect(desafios.progressoDoCliente).toHaveBeenCalledOnceWith('c1');
      expect(component.progressoDesafios).toEqual(progresso);
    });

    it('limpa a lista quando nenhum cliente está selecionado', () => {
      desafios.progressoDoCliente.and.returnValue(of(progresso));
      component.form.patchValue({ clienteId: 'c1' });

      component.form.patchValue({ clienteId: '' });

      expect(component.progressoDesafios).toEqual([]);
    });

    it('falha ao buscar o progresso não atrapalha o aluguel (lista vazia)', () => {
      desafios.progressoDoCliente.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));

      component.form.patchValue({ clienteId: 'c1' });

      expect(component.progressoDesafios).toEqual([]);
    });

    it('"Usar cupom" preenche o campo com o cupom liberado pela meta cumprida', () => {
      component.usarCupomDoDesafio('FIEL10');

      expect(component.form.get('cupomCodigo')?.value).toBe('FIEL10');
    });

    it('calcula o percentual da barra de progresso (limitado a 100%)', () => {
      expect(component.percentualDoDesafio(progresso[1])).toBe(30);
      expect(component.percentualDoDesafio({ ...progresso[0], progresso: 9 })).toBe(100);
      expect(component.percentualDoDesafio({ ...progresso[0], meta: 0 })).toBe(0);
    });

    it('mostra na tela o desafio cumprido com o botão do cupom e o em andamento com "3 de 10"', () => {
      desafios.progressoDoCliente.and.returnValue(of(progresso));
      component.form.patchValue({ clienteId: 'c1' });
      fixture.detectChanges();

      const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
      expect(texto).toContain('Meta cumprida');
      expect(texto).toContain('Usar cupom FIEL10');
      expect(texto).toContain('3 de 10 aluguéis');
    });
  });
  describe('condutores do cliente (consulta no servidor)', () => {
    it('ao escolher o cliente, carrega só os condutores dele', () => {
      component.form.patchValue({ clienteId: 'c1' });

      expect(condutores.listarPorCliente).toHaveBeenCalledOnceWith('c1');
      expect(component.condutoresFiltrados.map(c => c.id)).toEqual(['d1']);
    });

    it('trocar de cliente descarta o condutor que não é dele', () => {
      component.form.patchValue({ clienteId: 'c1' });
      component.form.patchValue({ condutorId: 'd1' });
      condutores.listarPorCliente.and.returnValue(of([]));

      component.form.patchValue({ clienteId: 'c2' });

      expect(component.form.get('condutorId')?.value).toBe('');
    });

    it('mantém o condutor escolhido quando ele pertence ao cliente (ex.: ao carregar a edição)', () => {
      component.form.patchValue({ clienteId: 'c1', condutorId: 'd1' });

      expect(component.form.get('condutorId')?.value).toBe('d1');
    });

    it('sem cliente a lista fica vazia e o campo pede para escolher o cliente primeiro', () => {
      component.form.patchValue({ clienteId: 'c1' });
      component.form.patchValue({ clienteId: '' });
      fixture.detectChanges();

      expect(component.condutoresFiltrados).toEqual([]);
      expect(fixture.nativeElement.querySelector('select[formControlName="condutorId"] option').textContent)
        .toContain('Selecione um cliente primeiro');
    });
  });
});
