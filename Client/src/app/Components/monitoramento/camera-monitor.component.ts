import { ChangeDetectionStrategy, Component, DestroyRef, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { NgIcon } from '@ng-icons/core';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { HlmSwitchImports } from '@spartan-ng/helm/switch';
import { interval } from 'rxjs';
import { mensagensDeErro } from '../../core/api-error';
import { NotificacaoService } from '../../core/notificacao.service';
import { EmptyStateComponent } from '../../shared/layout/empty-state.component';
import { ErrorStateComponent } from '../../shared/layout/error-state.component';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { StatusBadgeComponent } from '../../shared/layout/status-badge.component';
import { SelecionarAlugueisDto } from '../alugueis/aluguel.model';
import { AlugueisService } from '../alugueis/alugueis.service';
import { CameraService, DeteccaoPlaca } from './camera.service';
import { casarPlaca } from './placa-match';

export type EstadoTile = 'parada' | 'iniciando' | 'ao-vivo' | 'reconectando' | 'parando' | 'erro' | 'sem-permissao';

export interface CameraEstado {
  estado: EstadoTile;
  /** URL do stream MJPEG (com ticket de uso único); vazia enquanto não há vídeo. */
  url: string;
  tentativas: number;
  /** Mensagem real do servidor quando algo falhou (ex.: "Binário do Tesseract não encontrado"). */
  mensagem: string;
  /** Hora (ms) da última vez que a câmera ficou ao vivo. */
  aoVivoDesde: number | null;
}

const ESTADO_INICIAL: CameraEstado = { estado: 'parada', url: '', tentativas: 0, mensagem: '', aoVivoDesde: null };
const MAX_TENTATIVAS_STREAM = 4;
const MAX_PLACAS = 20;

/**
 * Monitoramento (SKILL §18 adaptado, ver §18.0): a fonte é a webcam do servidor de ML, entregue como MJPEG com ticket
 * de uso único (não há RTSP/WebRTC/NVR). Aqui entram: faixa de resumo, tiles com todos os estados (carregando, ao vivo,
 * reconectando com backoff, parada/offline, sem permissão, erro com a mensagem real), pausa do vídeo com a aba oculta,
 * liberação do processamento ao sair, e o painel de placas detectadas com a ação "Registrar devolução".
 */
@Component({
  selector: 'app-camera-monitor',
  imports: [
    FormsModule, NgIcon, HlmAlertImports, HlmButtonImports, HlmInputImports, HlmNativeSelectImports, HlmSwitchImports, PageComponent,
    PageHeaderComponent, StatusBadgeComponent, EmptyStateComponent, ErrorStateComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './camera-monitor.component.html'
})
export class CameraMonitorComponent implements OnInit {
  private readonly cameraService = inject(CameraService);
  private readonly aluguelService = inject(AlugueisService);
  private readonly router = inject(Router);
  private readonly notificacao = inject(NotificacaoService);
  private readonly destroyRef = inject(DestroyRef);

  readonly cameras = signal<number[]>([]);
  readonly carregandoCameras = signal(false);
  readonly erroCameras = signal('');
  readonly estados = signal<Record<number, CameraEstado>>({});

  readonly modoExibicao = signal<'unica' | 'multiplas'>('multiplas');
  readonly cameraSelecionada = signal<number | null>(null);
  /** Com a aba oculta o vídeo é solto (fecha a conexão MJPEG) e volta ao reexibir. */
  readonly abaVisivel = signal(true);

  readonly placasDetectadas = signal<DeteccaoPlaca[]>([]);
  readonly placasComAluguel = signal<ReadonlyMap<string, SelecionarAlugueisDto>>(new Map());
  readonly filtroPlaca = signal('');
  readonly filtroCamera = signal('');
  readonly apenasSemDevolucao = signal(false);

  private readonly placasJaProcessadas = new Set<string>();
  private readonly temporizadoresReconexao = new Map<number, ReturnType<typeof setTimeout>>();

  readonly camerasAtivas = computed(() => this.cameras().filter(c => this.estadoDe(c).estado !== 'parada' && this.estadoDe(c).estado !== 'erro' && this.estadoDe(c).estado !== 'sem-permissao'));
  readonly placasFiltradas = computed(() => {
    const termo = this.filtroPlaca().trim().toLowerCase();
    const camera = this.filtroCamera();
    return this.placasDetectadas().filter(
      p =>
        (!termo || p.plate.toLowerCase().includes(termo)) &&
        (camera === '' || p.camera === Number(camera)) &&
        (!this.apenasSemDevolucao() || !this.temAluguelEmAberto(p.plate))
    );
  });
  readonly placasNovas = computed(() => this.placasDetectadas().filter(p => !this.temAluguelEmAberto(p.plate)).length);
  readonly placasComAluguelTotal = computed(() => this.placasDetectadas().length - this.placasNovas());
  readonly tilesVisiveis = computed(() => {
    if (this.modoExibicao() === 'multiplas') return this.cameras();
    const escolhida = this.cameraSelecionada();
    return escolhida !== null ? [escolhida] : this.cameras().slice(0, 1);
  });

  ngOnInit(): void {
    this.carregarCameras();
    // Sem isto a lista de aluguéis só chegava no primeiro ciclo de 30 s (ou ao ligar uma câmera) e uma placa
    // lida logo ao abrir a tela aparecia como "Nova" mesmo tendo aluguel em aberto.
    this.atualizarInformacoesAlugueis();
    // A deduplicação por timestamp é feita no CameraService; aqui não há debounce porque ele
    // descartava placas de câmeras diferentes que chegavam juntas.
    this.cameraService.placasDetectadas$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(d => this.adicionarPlacaDetectada(d));
    interval(30000).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.atualizarInformacoesAlugueis());

    this.destroyRef.onDestroy(() => {
      this.temporizadoresReconexao.forEach(t => clearTimeout(t));
      this.cameraService.pararTodoMonitoramento();
      // Sair da tela desliga o processamento no servidor (antes a câmera ficava ligada). É só uma
      // chamada de limpeza: o componente já não existe, então não mexe no estado da tela.
      this.cameras()
        .filter(cam => this.estadoDe(cam).estado === 'ao-vivo' || this.estadoDe(cam).estado === 'reconectando' || this.estadoDe(cam).estado === 'iniciando')
        .forEach(cam => this.cameraService.pararCamera(cam).subscribe({ error: () => undefined }));
    });
  }

  // ========== CÂMERAS ==========

  estadoDe(camera: number): CameraEstado {
    return this.estados()[camera] ?? ESTADO_INICIAL;
  }

  private atualizar(camera: number, parcial: Partial<CameraEstado>): void {
    this.estados.update(e => ({ ...e, [camera]: { ...(e[camera] ?? ESTADO_INICIAL), ...parcial } }));
  }

  carregarCameras(): void {
    this.carregandoCameras.set(true);
    this.erroCameras.set('');
    this.cameraService.listarCameras().subscribe({
      next: resposta => {
        // O serviço de ML responde { cameras: number[], count: number }
        const lista = (resposta?.cameras ?? []).map(Number);
        this.cameras.set(lista);
        if (this.cameraSelecionada() === null && lista.length) this.cameraSelecionada.set(lista[0]);
        this.carregandoCameras.set(false);
      },
      error: err => {
        // Antes um erro "inventava" as câmeras [0, 1, 2, 3], que não existem: melhor admitir a falha.
        this.carregandoCameras.set(false);
        this.cameras.set([]);
        this.erroCameras.set(mensagensDeErro(err, 'Não foi possível consultar as câmeras do servidor.')[0]);
      }
    });
  }

  iniciarCamera(cameraIndex: number): void {
    this.atualizar(cameraIndex, { estado: 'iniciando', mensagem: '', tentativas: 0 });

    this.cameraService.iniciarCamera(cameraIndex).subscribe({
      next: () => {
        // As placas chegam pela assinatura única de ngOnInit; aqui só se liga o acompanhamento.
        this.cameraService.iniciarMonitoramento(cameraIndex);
        this.atualizar(cameraIndex, { estado: 'ao-vivo', aoVivoDesde: Date.now() });
        this.renovarStream(cameraIndex);
        this.atualizarInformacoesAlugueis();
      },
      error: (err: unknown) => {
        const status = (err as HttpErrorResponse)?.status;
        this.atualizar(cameraIndex, {
          estado: status === 401 || status === 403 ? 'sem-permissao' : 'erro',
          url: '',
          mensagem: mensagensDeErro(err, 'Verifique a conexão com o servidor.')[0]
        });
      }
    });
  }

  pararCamera(cameraIndex: number): void {
    this.cancelarReconexao(cameraIndex);
    this.atualizar(cameraIndex, { estado: 'parando' });
    this.cameraService.pararMonitoramento(cameraIndex);

    this.cameraService.pararCamera(cameraIndex).subscribe({
      next: () => this.finalizarParada(cameraIndex),
      error: err => {
        this.finalizarParada(cameraIndex);
        this.notificacao.aviso(`Câmera ${cameraIndex} parada localmente (erro no servidor: ${mensagensDeErro(err)[0]})`);
      }
    });
  }

  private finalizarParada(cameraIndex: number): void {
    this.atualizar(cameraIndex, { estado: 'parada', url: '', tentativas: 0, mensagem: '' });
    this.placasDetectadas.update(lista => lista.filter(p => p.camera !== cameraIndex));
  }

  alternarCamera(cameraIndex: number): void {
    const { estado } = this.estadoDe(cameraIndex);
    if (estado === 'iniciando' || estado === 'parando') return; // evita múltiplos cliques
    if (estado === 'ao-vivo' || estado === 'reconectando') {
      this.pararCamera(cameraIndex);
    } else {
      this.iniciarCamera(cameraIndex);
    }
  }

  pararTodasCameras(): void {
    this.cameras().forEach(cam => {
      const { estado } = this.estadoDe(cam);
      if (estado === 'ao-vivo' || estado === 'reconectando') this.pararCamera(cam);
    });
  }

  /** Pede um ticket novo e (re)aponta o <img> do stream. Cada ticket vale uma única conexão. */
  renovarStream(cameraIndex: number): void {
    this.cameraService.obterTicketStream().subscribe({
      next: ticket => this.atualizar(cameraIndex, { url: this.cameraService.obterStreamUrl(cameraIndex, ticket) }),
      error: err => {
        const status = (err as HttpErrorResponse)?.status;
        this.atualizar(cameraIndex, {
          estado: status === 401 || status === 403 ? 'sem-permissao' : 'erro',
          url: '',
          mensagem: `Não foi possível abrir o vídeo: ${mensagensDeErro(err)[0]}`
        });
      }
    });
  }

  /** O stream caiu (ticket já usado, servidor reiniciou...): reconecta com espera crescente (1 s, 2 s, 4 s...). */
  aoFalharStream(cameraIndex: number): void {
    const atual = this.estadoDe(cameraIndex);
    if (atual.estado !== 'ao-vivo' && atual.estado !== 'reconectando') return;

    const tentativas = atual.tentativas + 1;
    if (tentativas > MAX_TENTATIVAS_STREAM) {
      this.atualizar(cameraIndex, { estado: 'erro', url: '', tentativas, mensagem: 'O vídeo não carrega. Pare e inicie a câmera novamente.' });
      return;
    }
    this.atualizar(cameraIndex, { estado: 'reconectando', url: '', tentativas });
    this.agendarReconexao(cameraIndex, 1000 * 2 ** (tentativas - 1));
  }

  aoCarregarStream(cameraIndex: number): void {
    this.cancelarReconexao(cameraIndex);
    this.atualizar(cameraIndex, { estado: 'ao-vivo', tentativas: 0, mensagem: '', aoVivoDesde: Date.now() });
  }

  /** "Tentar agora": reconecta sem esperar o tempo do backoff. */
  tentarAgora(cameraIndex: number): void {
    this.cancelarReconexao(cameraIndex);
    this.renovarStream(cameraIndex);
  }

  private agendarReconexao(cameraIndex: number, esperaMs: number): void {
    this.cancelarReconexao(cameraIndex);
    this.temporizadoresReconexao.set(
      cameraIndex,
      setTimeout(() => {
        this.temporizadoresReconexao.delete(cameraIndex);
        if (this.estadoDe(cameraIndex).estado === 'reconectando') this.renovarStream(cameraIndex);
      }, esperaMs)
    );
  }

  private cancelarReconexao(cameraIndex: number): void {
    const t = this.temporizadoresReconexao.get(cameraIndex);
    if (t) clearTimeout(t);
    this.temporizadoresReconexao.delete(cameraIndex);
  }

  /** Aba oculta: solta o vídeo (fecha o MJPEG). Ao voltar, cada câmera ao vivo ganha um ticket novo. */
  @HostListener('document:visibilitychange')
  protected aoMudarVisibilidade(): void {
    const visivel = document.visibilityState === 'visible';
    this.abaVisivel.set(visivel);
    if (visivel) {
      this.cameras().filter(c => this.estadoDe(c).estado === 'ao-vivo').forEach(c => this.renovarStream(c));
    } else {
      this.cameras().filter(c => this.estadoDe(c).estado === 'ao-vivo').forEach(c => this.atualizar(c, { url: '' }));
    }
  }

  trocarModo(modo: 'unica' | 'multiplas'): void {
    this.modoExibicao.set(modo);
    // Trocar de modo recria os <img>; como o ticket é de uso único, cada vídeo ao vivo ganha um novo.
    this.cameras().filter(c => this.estadoDe(c).estado === 'ao-vivo').forEach(c => this.renovarStream(c));
  }

  selecionarCamera(cameraIndex: number): void {
    this.cameraSelecionada.set(cameraIndex);
    this.cameras().filter(c => this.estadoDe(c).estado === 'ao-vivo').forEach(c => this.renovarStream(c));
  }

  rotuloEstado(estado: EstadoTile): string {
    switch (estado) {
      case 'ao-vivo': return 'Ao vivo';
      case 'iniciando': return 'Ligando…';
      case 'reconectando': return 'Reconectando';
      case 'parando': return 'Parando…';
      case 'erro': return 'Erro';
      case 'sem-permissao': return 'Sem permissão';
      default: return 'Parada';
    }
  }

  // ========== PLACAS ==========

  adicionarPlacaDetectada(deteccao: DeteccaoPlaca): void {
    const chave = `${deteccao.plate}_${deteccao.camera}`;
    if (this.placasJaProcessadas.has(chave)) return;

    // Só interessa se a câmera ainda está ligada na tela
    const { estado } = this.estadoDe(deteccao.camera);
    if (estado !== 'ao-vivo' && estado !== 'reconectando') return;

    this.placasDetectadas.update(lista =>
      [{ ...deteccao, timestamp: deteccao.timestamp * 1000 /* o servidor manda segundos; a tela usa milissegundos */ }, ...lista].slice(0, MAX_PLACAS)
    );

    this.placasJaProcessadas.add(chave);
    setTimeout(() => this.placasJaProcessadas.delete(chave), 30000);

    this.buscarAluguelPorPlaca(deteccao.plate);
  }

  limparPlacas(): void {
    this.placasDetectadas.set([]);
  }

  // ========== ALUGUÉIS ==========

  atualizarInformacoesAlugueis(): void {
    this.aluguelService.selecionarEmAberto().subscribe({
      next: (alugueis: SelecionarAlugueisDto[]) => {
        const mapa = new Map<string, SelecionarAlugueisDto>();
        alugueis.forEach(a => a.automovelPlaca && mapa.set(a.automovelPlaca, a));
        this.placasComAluguel.set(mapa);
      },
      error: () => undefined   // o interceptor já avisa; a lista só é tentada de novo no próximo ciclo
    });
  }

  buscarAluguelPorPlaca(_placa: string): void {
    // O casamento placa lida × aluguel é feito em aluguelDaPlaca(); aqui só garantimos a lista em dia.
    this.atualizarInformacoesAlugueis();
  }

  /** Aluguel em aberto da placa lida: igual ou diferindo em 1 caractere que o OCR costuma confundir (R/K, B/8, O/0...). */
  aluguelDaPlaca(placa: string): SelecionarAlugueisDto | undefined {
    const mapa = this.placasComAluguel();
    const cadastrada = casarPlaca(placa, mapa.keys());
    return cadastrada ? mapa.get(cadastrada) : undefined;
  }

  /** Placa cadastrada quando a leitura da câmera difere dela (para avisar "lida X, cadastrada Y"). */
  placaCadastradaDiferente(placa: string): string | null {
    const aluguel = this.aluguelDaPlaca(placa);
    const cadastrada = aluguel?.automovelPlaca;
    return cadastrada && cadastrada.toUpperCase() !== placa.toUpperCase() ? cadastrada : null;
  }

  temAluguelEmAberto(placa: string): boolean {
    return this.aluguelDaPlaca(placa) !== undefined;
  }

  getAluguelInfo(placa: string): SelecionarAlugueisDto | undefined {
    return this.aluguelDaPlaca(placa);
  }

  navegarParaDevolucao(placa: string): void {
    const aluguel = this.aluguelDaPlaca(placa);
    if (aluguel?.id) {
      void this.router.navigate(['/alugueis', aluguel.id, 'devolver']);
      return;
    }
    this.aluguelService.selecionarEmAberto().subscribe({
      next: (alugueis: SelecionarAlugueisDto[]) => {
        const cadastrada = casarPlaca(placa, alugueis.map(a => a.automovelPlaca ?? ''));
        const doVeiculo = cadastrada ? alugueis.find(a => a.automovelPlaca?.toUpperCase().replace(/[^A-Z0-9]/g, '') === cadastrada) : undefined;
        if (doVeiculo?.id) {
          void this.router.navigate(['/alugueis', doVeiculo.id, 'devolver']);
        } else {
          this.notificacao.aviso(`Nenhum aluguel em aberto encontrado para a placa ${placa}`);
        }
      },
      error: () => undefined   // o interceptor já avisou o motivo
    });
  }

  // ========== UTILITÁRIOS ==========

  formatarHora(timestamp: number): string {
    return new Date(timestamp).toLocaleTimeString('pt-BR');
  }

  formatarDataString(dataString?: string): string {
    if (!dataString) return '—';
    const data = new Date(dataString);
    return Number.isNaN(data.getTime()) ? '—' : data.toLocaleDateString('pt-BR');
  }

  telaCheia(elemento: HTMLElement): void {
    if (document.fullscreenElement) {
      void document.exitFullscreen();
    } else {
      void elemento.requestFullscreen?.();
    }
  }
}
