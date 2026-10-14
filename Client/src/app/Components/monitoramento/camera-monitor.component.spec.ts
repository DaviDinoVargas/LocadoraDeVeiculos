import { ComponentFixture, TestBed, fakeAsync, tick, discardPeriodicTasks } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { NotificacaoService } from '../../core/notificacao.service';
import { criarNotificacaoFalsa } from '../../core/testing/notificacao-fake.spec';
import { SelecionarAlugueisDto } from '../alugueis/aluguel.model';
import { AlugueisService } from '../alugueis/alugueis.service';
import { CameraMonitorComponent } from './camera-monitor.component';
import { CameraService, DeteccaoPlaca } from './camera.service';
import { violacoesNosDoisTemas } from '../../core/testing/acessibilidade';

describe('CameraMonitorComponent — câmeras, placas e devolução', () => {
  let fixture: ComponentFixture<CameraMonitorComponent>;
  let component: CameraMonitorComponent;
  let camera: jasmine.SpyObj<CameraService>;
  let alugueis: jasmine.SpyObj<AlugueisService>;
  let notificacao: jasmine.SpyObj<NotificacaoService>;
  let router: Router;
  let placas$: Subject<DeteccaoPlaca>;

  const aluguel = (placa: string, id = 'a-' + placa): SelecionarAlugueisDto => ({
    id, condutorNome: 'Ana', automovelPlaca: placa, clienteNome: 'Cliente', dataSaida: '2026-10-10T10:00:00Z',
    dataRetornoPrevisto: '2026-10-13T10:00:00Z', valorPrevisto: 300, status: 'EmAndamento'
  });
  const deteccao = (plate: string, cam = 0): DeteccaoPlaca => ({ camera: cam, plate, bbox: [0, 0, 1, 1], timestamp: 1_700_000_000, confidence: 0.9 });

  const montar = (aluguelAbertos: SelecionarAlugueisDto[] = [aluguel('BRA2E19')]) => {
    placas$ = new Subject<DeteccaoPlaca>();
    camera = jasmine.createSpyObj('CameraService', [
      'listarCameras', 'iniciarCamera', 'pararCamera', 'iniciarMonitoramento', 'pararMonitoramento', 'pararTodoMonitoramento',
      'obterTicketStream', 'obterStreamUrl'
    ], { placasDetectadas$: placas$.asObservable() });
    camera.listarCameras.and.returnValue(of({ cameras: [0], count: 1 }));
    camera.iniciarCamera.and.returnValue(of({ ok: true, camera: 0, fps: 10 }));
    camera.pararCamera.and.returnValue(of({ ok: true, camera: 0 }));
    camera.obterTicketStream.and.returnValue(of('tk'));
    camera.obterStreamUrl.and.callFake((i: number, t: string) => `/stream/${i}?ticket=${t}`);
    alugueis = jasmine.createSpyObj('AlugueisService', ['selecionarEmAberto']);
    alugueis.selecionarEmAberto.and.returnValue(of(aluguelAbertos));
    notificacao = criarNotificacaoFalsa();

    TestBed.configureTestingModule({
      imports: [CameraMonitorComponent],
      providers: [
        provideRouter([]),
        { provide: CameraService, useValue: camera },
        { provide: AlugueisService, useValue: alugueis },
        { provide: NotificacaoService, useValue: notificacao }
      ]
    });
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(CameraMonitorComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  };

  afterEach(() => fixture?.destroy());

  describe('ao abrir a tela', () => {
    it('lista as câmeras do servidor e já carrega os aluguéis em aberto (sem esperar o ciclo de 30 s)', () => {
      montar();

      expect(component.cameras()).toEqual([0]);
      expect(component.cameraSelecionada()).toBe(0);
      expect(alugueis.selecionarEmAberto).toHaveBeenCalledTimes(1);
      expect(component.placasComAluguel().has('BRA2E19')).toBeTrue();
    });

    it('falha ao consultar as câmeras mostra o erro e não inventa câmeras', () => {
      montar();
      camera.listarCameras.and.returnValue(throwError(() => ({ status: 500, error: ['ML fora do ar'] })));

      component.carregarCameras();

      expect(component.cameras()).toEqual([]);
      expect(component.erroCameras()).toBe('ML fora do ar');
    });

    it('o filtro "só placas sem aluguel em aberto" vem desligado (senão esconderia as placas a devolver)', () => {
      montar();

      expect(component.apenasSemDevolucao()).toBeFalse();
    });

    it('erro ao buscar aluguéis não quebra a tela', () => {
      montar();
      alugueis.selecionarEmAberto.and.returnValue(throwError(() => ({ status: 401 })));

      expect(() => component.atualizarInformacoesAlugueis()).not.toThrow();
    });
  });

  describe('estado das câmeras', () => {
    it('iniciar: liga, acompanha as placas e fica ao vivo com o ticket do stream', () => {
      montar();

      component.alternarCamera(0);

      expect(camera.iniciarCamera).toHaveBeenCalledWith(0);
      expect(camera.iniciarMonitoramento).toHaveBeenCalledWith(0);
      expect(component.estadoDe(0).estado).toBe('ao-vivo');
      expect(component.estadoDe(0).url).toBe('/stream/0?ticket=tk');
    });

    it('401 ao iniciar vira "sem permissão"; outro erro vira "erro" com a mensagem real', () => {
      montar();
      camera.iniciarCamera.and.returnValue(throwError(() => ({ status: 401, error: ['Sessão expirada'] })));
      component.iniciarCamera(0);
      expect(component.estadoDe(0).estado).toBe('sem-permissao');

      camera.iniciarCamera.and.returnValue(throwError(() => ({ status: 503, error: ['Tesseract não encontrado'] })));
      component.iniciarCamera(0);
      expect(component.estadoDe(0).estado).toBe('erro');
      expect(component.estadoDe(0).mensagem).toBe('Tesseract não encontrado');
    });

    it('alternar uma câmera ao vivo para o processamento; durante "iniciando" o clique é ignorado', () => {
      montar();
      component.iniciarCamera(0);

      component.alternarCamera(0);
      expect(camera.pararCamera).toHaveBeenCalledWith(0);
      expect(component.estadoDe(0).estado).toBe('parada');

      component['atualizar'](0, { estado: 'iniciando' });
      camera.iniciarCamera.calls.reset();
      component.alternarCamera(0);
      expect(camera.iniciarCamera).not.toHaveBeenCalled();
    });

    it('erro do servidor ao parar ainda para localmente e avisa', () => {
      montar();
      component.iniciarCamera(0);
      camera.pararCamera.and.returnValue(throwError(() => ({ status: 500, error: ['falhou'] })));

      component.pararCamera(0);

      expect(component.estadoDe(0).estado).toBe('parada');
      expect(notificacao.aviso).toHaveBeenCalled();
    });

    it('stream que cai reconecta com espera crescente (1 s, 2 s, 4 s) e desiste na 5ª tentativa', fakeAsync(() => {
      montar();
      component.iniciarCamera(0);
      camera.obterTicketStream.calls.reset();

      component.aoFalharStream(0);
      expect(component.estadoDe(0).estado).toBe('reconectando');
      tick(999);
      expect(camera.obterTicketStream).not.toHaveBeenCalled();
      tick(1);
      expect(camera.obterTicketStream).toHaveBeenCalledTimes(1);

      component.aoFalharStream(0);   // 2ª: 2 s
      tick(2000);
      component.aoFalharStream(0);   // 3ª: 4 s
      tick(4000);
      component.aoFalharStream(0);   // 4ª: 8 s
      tick(8000);
      component.aoFalharStream(0);   // 5ª: passa do limite

      expect(component.estadoDe(0).estado).toBe('erro');
      expect(component.estadoDe(0).mensagem).toContain('Pare e inicie');
      discardPeriodicTasks();
    }));

    it('vídeo que carrega zera as tentativas e volta a "ao vivo"', () => {
      montar();
      component.iniciarCamera(0);
      component.aoFalharStream(0);

      component.aoCarregarStream(0);

      expect(component.estadoDe(0).estado).toBe('ao-vivo');
      expect(component.estadoDe(0).tentativas).toBe(0);
    });

    it('"tentar agora" pede um ticket novo sem esperar o backoff', () => {
      montar();
      component.iniciarCamera(0);
      camera.obterTicketStream.calls.reset();

      component.tentarAgora(0);

      expect(camera.obterTicketStream).toHaveBeenCalledTimes(1);
    });

    it('aba oculta solta o vídeo; ao voltar pede ticket novo', () => {
      montar();
      component.iniciarCamera(0);
      const visibilidade = spyOnProperty(document, 'visibilityState').and.returnValue('hidden');

      component['aoMudarVisibilidade']();
      expect(component.estadoDe(0).url).toBe('');
      expect(component.abaVisivel()).toBeFalse();

      camera.obterTicketStream.calls.reset();
      visibilidade.and.returnValue('visible');
      component['aoMudarVisibilidade']();

      expect(component.abaVisivel()).toBeTrue();
      expect(camera.obterTicketStream).toHaveBeenCalledTimes(1);
      expect(component.estadoDe(0).url).toBe('/stream/0?ticket=tk');
    });

    it('sair da tela para o monitoramento e desliga as câmeras ligadas', () => {
      montar();
      component.iniciarCamera(0);
      camera.pararCamera.calls.reset();

      fixture.destroy();

      expect(camera.pararTodoMonitoramento).toHaveBeenCalled();
      expect(camera.pararCamera).toHaveBeenCalledWith(0);
    });
  });

  describe('placas detectadas', () => {
    beforeEach(() => {
      montar();
      component.iniciarCamera(0);
    });

    it('placa lida entra na lista (mais recente primeiro), com o timestamp em ms', () => {
      placas$.next(deteccao('ABC1D30'));
      placas$.next({ ...deteccao('XYZ9Z99'), timestamp: 1_700_000_001 });

      expect(component.placasDetectadas().map(p => p.plate)).toEqual(['XYZ9Z99', 'ABC1D30']);
      expect(component.placasDetectadas()[0].timestamp).toBe(1_700_000_001 * 1000);
    });

    it('placa de câmera que não está ligada é ignorada', () => {
      component.pararCamera(0);

      placas$.next(deteccao('ABC1D30'));

      expect(component.placasDetectadas()).toEqual([]);
    });

    it('guarda no máximo as 20 placas mais recentes', () => {
      for (let i = 0; i < 25; i++) {
        component['placasJaProcessadas'].clear();
        placas$.next({ ...deteccao('ABC1D3' + (i % 10)), timestamp: 1_700_000_000 + i });
      }

      expect(component.placasDetectadas().length).toBe(20);
    });

    it('a mesma placa na mesma câmera não é adicionada duas vezes em seguida', () => {
      placas$.next(deteccao('ABC1D30'));
      placas$.next(deteccao('ABC1D30'));

      expect(component.placasDetectadas().length).toBe(1);
    });

    it('filtro por texto e por câmera', () => {
      placas$.next(deteccao('ABC1D30'));
      placas$.next({ ...deteccao('XYZ9Z99'), timestamp: 1_700_000_001 });

      component.filtroPlaca.set('xyz');
      expect(component.placasFiltradas().map(p => p.plate)).toEqual(['XYZ9Z99']);

      component.filtroPlaca.set('');
      component.filtroCamera.set('3');
      expect(component.placasFiltradas()).toEqual([]);
    });

    it('limpar a lista esvazia as placas', () => {
      placas$.next(deteccao('ABC1D30'));

      component.limparPlacas();

      expect(component.placasDetectadas()).toEqual([]);
    });
  });

  describe('placa × aluguel em aberto (tolerando confusão de OCR)', () => {
    beforeEach(() => montar([aluguel('BRA2E19'), aluguel('ABC1D30')]));

    it('placa exata encontra o aluguel', () => {
      expect(component.temAluguelEmAberto('BRA2E19')).toBeTrue();
      expect(component.getAluguelInfo('BRA2E19')?.id).toBe('a-BRA2E19');
      expect(component.placaCadastradaDiferente('BRA2E19')).toBeNull();
    });

    it('R lido como K encontra o aluguel e mostra a placa cadastrada', () => {
      expect(component.temAluguelEmAberto('BKA2E19')).toBeTrue();
      expect(component.placaCadastradaDiferente('BKA2E19')).toBe('BRA2E19');
    });

    it('placa que não tem aluguel é "nova"', () => {
      expect(component.temAluguelEmAberto('QQQ0Q00')).toBeFalse();
    });

    it('contadores separam placas com aluguel das novas', () => {
      component.placasDetectadas.set([
        { ...deteccao('BKA2E19'), timestamp: 1 },
        { ...deteccao('QQQ0Q00'), timestamp: 2 }
      ]);

      expect(component.placasComAluguelTotal()).toBe(1);
      expect(component.placasNovas()).toBe(1);
    });

    it('com o filtro ligado, esconde as placas que já têm aluguel', () => {
      component.placasDetectadas.set([{ ...deteccao('BRA2E19'), timestamp: 1 }, { ...deteccao('QQQ0Q00'), timestamp: 2 }]);

      component.apenasSemDevolucao.set(true);

      expect(component.placasFiltradas().map(p => p.plate)).toEqual(['QQQ0Q00']);
    });

    it('"Registrar devolução" abre a devolução do aluguel certo (mesmo com a leitura aproximada)', () => {
      component.navegarParaDevolucao('BKA2E19');

      expect(router.navigate).toHaveBeenCalledWith(['/alugueis', 'a-BRA2E19', 'devolver']);
    });

    it('sem aluguel para a placa, busca de novo e avisa com a placa lida', () => {
      alugueis.selecionarEmAberto.and.returnValue(of([]));

      component.navegarParaDevolucao('QQQ0Q00');

      expect(notificacao.aviso).toHaveBeenCalledWith('Nenhum aluguel em aberto encontrado para a placa QQQ0Q00');
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('lista de aluguéis ainda não carregada: a busca de última hora resolve o clique', () => {
      component.placasComAluguel.set(new Map());
      alugueis.selecionarEmAberto.and.returnValue(of([aluguel('BRA2E19')]));

      component.navegarParaDevolucao('BRA2E19');

      expect(router.navigate).toHaveBeenCalledWith(['/alugueis', 'a-BRA2E19', 'devolver']);
    });
  });

  describe('modos de exibição', () => {
    it('modo "uma câmera" mostra só a selecionada', () => {
      montar();
      camera.listarCameras.and.returnValue(of({ cameras: [0, 1], count: 2 }));
      component.carregarCameras();

      expect(component.tilesVisiveis()).toEqual([0, 1]);   // padrão: múltiplas
      component.trocarModo('unica');
      component.selecionarCamera(1);

      expect(component.tilesVisiveis()).toEqual([1]);
    });

    it('rótulos de estado em português', () => {
      montar();

      expect(component.rotuloEstado('ao-vivo')).toBe('Ao vivo');
      expect(component.rotuloEstado('sem-permissao')).toBe('Sem permissão');
      expect(component.rotuloEstado('parada')).toBe('Parada');
    });
  });

  it('acessibilidade (axe-core, WCAG 2.2 AA): sem violações nos temas claro e escuro', async () => {
    montar();
    fixture.detectChanges();
    // a tela tem um ciclo de 30 s: whenStable() nunca resolveria, então basta um turno do navegador
    await new Promise(resolver => setTimeout(resolver));
    fixture.detectChanges();
    expect(await violacoesNosDoisTemas(fixture.nativeElement)).toEqual([]);
  });
});
