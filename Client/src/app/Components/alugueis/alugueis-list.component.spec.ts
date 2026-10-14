import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { NotificacaoService } from '../../core/notificacao.service';
import { criarNotificacaoFalsa } from '../../core/testing/notificacao-fake.spec';
import { of, throwError } from 'rxjs';
import { AlugueisListComponent } from './alugueis-list.component';
import { AlugueisService } from './alugueis.service';
import { DevolucoesService } from '../devolucoes/devolucoes.service';
import { SelecionarAlugueisDto } from './aluguel.model';
import { DevolucaoCompletoDto } from '../devolucoes/devolucao.model';
import { PdfService } from '../pdf/pdf.service';
import { violacoesNosDoisTemas } from '../../core/testing/acessibilidade';

describe('AlugueisListComponent — filtro de status no servidor e devolução', () => {
  let fixture: ComponentFixture<AlugueisListComponent>;
  let component: AlugueisListComponent;
  let alugueis: jasmine.SpyObj<AlugueisService>;
  let devolucoes: jasmine.SpyObj<DevolucoesService>;
  let notificacao: jasmine.SpyObj<NotificacaoService>;
  let router: Router;

  const aluguel = (id: string, status: string) => ({ id, status, automovelPlaca: 'AAA1A11', clienteNome: 'Fulano', condutorNome: 'Beltrano' }) as SelecionarAlugueisDto;

  beforeEach(() => {
    alugueis = jasmine.createSpyObj('AlugueisService', ['listar', 'listarPorStatus', 'selecionarEmAberto']);
    alugueis.listar.and.returnValue(of([aluguel('1', 'Reservado'), aluguel('2', 'Concluido')]));
    alugueis.listarPorStatus.and.returnValue(of([aluguel('2', 'Concluido')]));
    alugueis.selecionarEmAberto.and.returnValue(of([aluguel('1', 'Reservado')]));
    devolucoes = jasmine.createSpyObj('DevolucoesService', ['obterPorAluguelId']);
    notificacao = criarNotificacaoFalsa();

    TestBed.configureTestingModule({
      imports: [AlugueisListComponent],
      providers: [
        provideRouter([]),
        { provide: AlugueisService, useValue: alugueis },
        { provide: DevolucoesService, useValue: devolucoes },
        { provide: NotificacaoService, useValue: notificacao }
      ]
    });
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(AlugueisListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('abre listando todos', () => {
    expect(alugueis.listar).toHaveBeenCalledTimes(1);
    expect(component.alugueisFiltrados().length).toBe(2);
  });

  it('um status específico consulta GET /alugueis/status/{status} em vez de filtrar na memória', () => {
    component.filtrarPorStatus('Concluido');

    expect(alugueis.listarPorStatus).toHaveBeenCalledOnceWith('Concluido');
    expect(component.alugueisFiltrados().map(a => a.id)).toEqual(['2']);
  });

  it('"em aberto" usa o endpoint próprio e "todos" volta a listar tudo', () => {
    component.filtrarPorStatus('em-aberto');
    expect(alugueis.selecionarEmAberto).toHaveBeenCalledTimes(1);
    expect(component.alugueisFiltrados().map(a => a.id)).toEqual(['1']);

    component.filtrarPorStatus('todos');
    expect(alugueis.listar).toHaveBeenCalledTimes(2);
  });

  it('erro ao filtrar mostra o estado de erro (com "Tentar novamente") e desliga o carregando', () => {
    alugueis.listarPorStatus.and.returnValue(throwError(() => new Error('falhou')));

    component.filtrarPorStatus('Cancelado');
    fixture.detectChanges();

    expect(component.erro()).toBeTrue();
    expect(component.carregando()).toBeFalse();
    expect(fixture.nativeElement.textContent).toContain('Tentar novamente');
  });

  it('só aluguel concluído tem a ação "Ver devolução"; só reservado/em andamento pode cancelar', () => {
    const acao = (rotulo: string) => component.acoes().find(a => a.rotulo === rotulo)!;

    expect(acao('Ver devolução').visivel!(aluguel('2', 'Concluido'))).toBeTrue();
    expect(acao('Ver devolução').visivel!(aluguel('1', 'Reservado'))).toBeFalse();
    expect(acao('Cancelar aluguel').visivel!(aluguel('1', 'Reservado'))).toBeTrue();
    expect(acao('Cancelar aluguel').visivel!(aluguel('2', 'Concluido'))).toBeFalse();
    expect(acao('Registrar devolução').visivel!(aluguel('3', 'EmAndamento'))).toBeTrue();
  });

  it('"Ver devolução" abre a devolução registrada do aluguel', () => {
    devolucoes.obterPorAluguelId.and.returnValue(of({ id: 'dev-9' } as unknown as DevolucaoCompletoDto));

    component.verDevolucao('2');

    expect(devolucoes.obterPorAluguelId).toHaveBeenCalledOnceWith('2');
    expect(router.navigate).toHaveBeenCalledWith(['/devolucoes/dev-9']);
  });

  it('sem devolução registrada, avisa em vez de navegar', () => {
    devolucoes.obterPorAluguelId.and.returnValue(throwError(() => new Error('404')));

    component.verDevolucao('2');

    expect(notificacao.aviso).toHaveBeenCalledWith('Este aluguel ainda não tem devolução registrada.');
    expect(router.navigate).not.toHaveBeenCalled();
  });
  describe('exportar PDF', () => {
    const botao = () =>
      Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('button')).find(b => b.textContent?.includes('Exportar PDF'))!;

    it('exporta exatamente a lista que está na tela, com o nome do filtro', () => {
      const gerar = spyOn(TestBed.inject(PdfService), 'gerarRelatorioAlugueis');
      component.filtrarPorStatus('Concluido');
      fixture.detectChanges();

      botao().click();

      expect(gerar).toHaveBeenCalledOnceWith(component.alugueisFiltrados(), 'Concluídos');
    });

    it('com a lista vazia o botão fica desabilitado e nada é exportado', () => {
      const gerar = spyOn(TestBed.inject(PdfService), 'gerarRelatorioAlugueis');
      alugueis.listarPorStatus.and.returnValue(of([]));
      component.filtrarPorStatus('Cancelado');
      fixture.detectChanges();

      expect(botao().disabled).toBeTrue();
      component.exportarPdf();
      expect(gerar).not.toHaveBeenCalled();
    });
  });

  it('acessibilidade (axe-core, WCAG 2.2 AA): sem violações nos temas claro e escuro', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(await violacoesNosDoisTemas(fixture.nativeElement)).toEqual([]);
  });
});
