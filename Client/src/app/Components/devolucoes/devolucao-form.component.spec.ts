import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { NotificacaoService } from '../../core/notificacao.service';
import { criarNotificacaoFalsa } from '../../core/testing/notificacao-fake.spec';
import { AluguelCompletoDto } from '../alugueis/aluguel.model';
import { AlugueisService } from '../alugueis/alugueis.service';
import { ConfiguracoesService } from '../configuracoes/configuracoes.service';
import { DevolucaoFormComponent } from './devolucao-form.component';
import { DevolucaoDto } from './devolucao.model';
import { DevolucoesService } from './devolucoes.service';

describe('DevolucaoFormComponent — cálculo da devolução e envio', () => {
  let fixture: ComponentFixture<DevolucaoFormComponent>;
  let component: DevolucaoFormComponent;
  let devolucoes: jasmine.SpyObj<DevolucoesService>;
  let alugueis: jasmine.SpyObj<AlugueisService>;
  let notificacao: jasmine.SpyObj<NotificacaoService>;
  let router: Router;

  const aluguel: AluguelCompletoDto = {
    id: 'a1', condutorId: 'c', automovelId: 'v', clienteId: 'cl',
    dataSaida: '2026-10-08T10:00:00Z', dataRetornoPrevisto: '2026-10-10T10:00:00Z',
    valorPrevisto: 300, status: 'EmAndamento', taxasServicosIds: [],
    taxasServicos: [{ id: 't1', nome: 'GPS', preco: 20, tipoCalculo: 'Fixa' }],
    condutorNome: 'Ana', automovelPlaca: 'BRA2E19', clienteNome: 'Cliente'
  };

  const montar = (queryParams: Record<string, string> = {}) => {
    devolucoes = jasmine.createSpyObj('DevolucoesService', ['registrar']);
    alugueis = jasmine.createSpyObj('AlugueisService', ['obter', 'selecionarEmAberto']);
    alugueis.obter.and.returnValue(of(aluguel));
    alugueis.selecionarEmAberto.and.returnValue(of([]));
    notificacao = criarNotificacaoFalsa();

    TestBed.configureTestingModule({
      imports: [DevolucaoFormComponent],
      providers: [
        provideRouter([]),
        { provide: DevolucoesService, useValue: devolucoes },
        { provide: AlugueisService, useValue: alugueis },
        { provide: ConfiguracoesService, useValue: { obter: () => of({ precoCombustivel: 6, valorGarantia: 1000 }) } },
        { provide: NotificacaoService, useValue: notificacao },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'a1' }) }, queryParams: of(queryParams) } }
      ]
    });
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(DevolucaoFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  };

  it('carrega o aluguel e a configuração do id da rota', () => {
    montar();

    expect(alugueis.obter).toHaveBeenCalledWith('a1');
    expect(component.aluguel()?.automovelPlaca).toBe('BRA2E19');
    expect(component.configuracao()?.precoCombustivel).toBe(6);
  });

  it('se o aluguel não carrega, volta para a lista de aluguéis', () => {
    montar();
    alugueis.obter.and.returnValue(throwError(() => ({ status: 404 })));

    component.carregarAluguel('x');

    expect(router.navigate).toHaveBeenCalledWith(['/alugueis']);
  });

  it('devolução no prazo: sem multa; total = valor previsto + taxas', () => {
    montar();
    component.form.patchValue({ dataDevolucao: '2026-10-09', quilometragemFinal: 100, nivelCombustivel: 'Cheio', combustivelNoTanque: 50 });

    expect(component.multa()).toBe(0);
    expect(component.valorTaxas()).toBe(20);
    expect(component.valorTotal()).toBe(320);
  });

  it('devolução atrasada: multa de 10% do valor previsto', () => {
    montar();
    component.form.patchValue({ dataDevolucao: '2026-10-12', nivelCombustivel: 'Cheio', combustivelNoTanque: 50 });

    expect(component.multa()).toBe(30);
    expect(component.valorTotal()).toBe(350);
  });

  it('combustível abaixo do nível informado gera adicional (litros que faltam × preço do litro)', () => {
    montar();
    component.form.patchValue({ dataDevolucao: '2026-10-09', nivelCombustivel: 'Cheio', combustivelNoTanque: 30 });

    expect(component.valorCombustivel()).toBe(120);   // (50 - 30) × 6
    expect(component.valorTotal()).toBe(300 + 20 + 120);
  });

  it('nível Metade espera 25 L: acima disso não cobra', () => {
    montar();
    component.form.patchValue({ dataDevolucao: '2026-10-09', nivelCombustivel: 'Metade', combustivelNoTanque: 25 });

    expect(component.valorCombustivel()).toBe(0);
  });

  it('formulário vazio não chama a API: avisa e marca os campos', () => {
    montar();

    component.salvar();

    expect(devolucoes.registrar).not.toHaveBeenCalled();
    expect(notificacao.aviso).toHaveBeenCalledWith('Verifique os campos destacados.');
    expect(component.form.controls.dataDevolucao.touched).toBeTrue();
  });

  it('envio válido manda o cálculo no payload, avisa e vai para a devolução criada', () => {
    montar();
    devolucoes.registrar.and.returnValue(of({ id: 'd9' } as unknown as DevolucaoDto));
    component.form.patchValue({ dataDevolucao: '2026-10-12', quilometragemFinal: 500, nivelCombustivel: 'Cheio', combustivelNoTanque: 30 });

    component.salvar();

    const payload = devolucoes.registrar.calls.mostRecent().args[0];
    expect(payload.aluguelId).toBe('a1');
    expect(payload.valorMultas).toBe(30);
    expect(payload.valorAdicionalCombustivel).toBe(120);
    expect(payload.valorTotal).toBe(470);
    expect(notificacao.sucesso).toHaveBeenCalledWith('Devolução registrada com sucesso');
    expect(router.navigate).toHaveBeenCalledWith(['/devolucoes', 'd9']);
    expect(component.salvando()).toBeFalse();
  });

  it('erro do servidor mostra a mensagem real e libera o botão', () => {
    montar();
    devolucoes.registrar.and.returnValue(throwError(() => ({ status: 409, error: ['Aluguel já devolvido.'] })));
    component.form.patchValue({ dataDevolucao: '2026-10-09', quilometragemFinal: 10, nivelCombustivel: 'Cheio', combustivelNoTanque: 50 });

    component.salvar();

    expect(component.erros()).toEqual(['Aluguel já devolvido.']);
    expect(component.salvando()).toBeFalse();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('com ?placa= na URL, procura o aluguel em aberto daquela placa', () => {
    montar({ placa: 'BRA2E19' });

    expect(alugueis.selecionarEmAberto).toHaveBeenCalled();
    expect(notificacao.aviso).toHaveBeenCalledWith('Nenhum aluguel em aberto encontrado para a placa BRA2E19');
  });

  it('cancelar volta para os aluguéis', () => {
    montar();

    component.cancelar();

    expect(router.navigate).toHaveBeenCalledWith(['/alugueis']);
  });
});
