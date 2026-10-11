import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { Router } from '@angular/router';
import { CameraService, DeteccaoPlaca } from './camera.service';
import { AlugueisService } from '../alugueis/alugueis.service';
import { SelecionarAlugueisDto } from '../alugueis/aluguel.model';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription, interval } from 'rxjs';
import { MatSnackBar } from '@angular/material/snack-bar';
import { mensagensDeErro } from '../../core/api-error';

@Component({
  selector: 'app-camera-monitor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './camera-monitor.component.html',
  styleUrls: ['./camera-monitor.component.scss']
})
export class CameraMonitorComponent implements OnInit, OnDestroy {
  private readonly snack = inject(MatSnackBar);

  // Câmeras: DEVE SER UM ARRAY de números
  cameras: number[] = [];

  // Estado das câmeras
  camerasAtivas: Map<number, boolean> = new Map();
  camerasStatus: Map<number, string> = new Map(); // 'starting', 'running', 'stopping', 'stopped'

  // Placas detectadas
  placasDetectadas: DeteccaoPlaca[] = [];
  placasFiltradas: DeteccaoPlaca[] = [];

  // Controle de exibição
  modoExibicao: 'unica' | 'multiplas' = 'multiplas';
  cameraSelecionada: number | null = null;

  // Filtros
  filtroPlaca: string = '';
  filtroCamera: number | null = null;
  apenasNaoProcessadas: boolean = true;

  // URLs do stream MJPEG por câmera (levam um ticket de uso único, ver CameraService.obterTicketStream)
  streamUrls: Map<number, string> = new Map();
  private tentativasStream: Map<number, number> = new Map();

  // Estado
  loading = false;
  erroCameras = '';
  loadingCameras: Map<number, boolean> = new Map();
  placasComAluguel: Map<string, SelecionarAlugueisDto> = new Map();

  // Subscrições
  private subscriptions: Subscription[] = [];
  private placasJaProcessadas: Set<string> = new Set();

  constructor(
    private cameraService: CameraService,
    private aluguelService: AlugueisService,
    private router: Router
  ) {}

  ngOnInit() {
    this.carregarCameras();
    this.iniciarMonitoramentoDetecoes();
    this.iniciarAtualizacaoAlugueis();
  }

  ngOnDestroy() {
    this.subscriptions.forEach(sub => sub.unsubscribe());
    this.cameraService.pararTodoMonitoramento();
    // Sair da tela desliga o processamento no servidor (antes a câmera ficava ligada). É só uma
    // chamada de limpeza: o componente já não existe, então não mexe no estado da tela.
    this.cameras
      .filter(cam => this.camerasAtivas.get(cam))
      .forEach(cam => this.cameraService.pararCamera(cam).subscribe({ error: () => undefined }));
  }

  // ========== MÉTODOS DE CÂMERAS ==========

  carregarCameras() {
    this.loading = true;
    this.erroCameras = '';
    this.cameraService.listarCameras().subscribe({
      next: (response) => {
        // O serviço de ML responde { cameras: number[], count: number }
        this.cameras = (response?.cameras ?? []).map(Number);

        this.cameras.forEach(cam => {
          if (!this.camerasAtivas.has(cam)) {
            this.camerasAtivas.set(cam, false);
            this.camerasStatus.set(cam, 'stopped');
            this.loadingCameras.set(cam, false);
          }
        });

        this.loading = false;
      },
      error: (err) => {
        // Antes um erro "inventava" as câmeras [0, 1, 2, 3], que não existem: melhor admitir a falha.
        this.loading = false;
        this.cameras = [];
        this.erroCameras = mensagensDeErro(err, 'Não foi possível consultar as câmeras do servidor.')[0];
      }
    });
  }

  iniciarCamera(cameraIndex: number) {
    this.loadingCameras.set(cameraIndex, true);
    this.camerasStatus.set(cameraIndex, 'starting');

    this.cameraService.iniciarCamera(cameraIndex).subscribe({
      next: () => {
        this.camerasAtivas.set(cameraIndex, true);
        this.camerasStatus.set(cameraIndex, 'running');
        this.loadingCameras.set(cameraIndex, false);

        // As placas chegam pela assinatura única de ngOnInit; aqui só se liga o acompanhamento.
        this.cameraService.iniciarMonitoramento(cameraIndex);
        this.renovarStream(cameraIndex);
      },
      error: (err) => {
        this.camerasAtivas.set(cameraIndex, false);
        this.camerasStatus.set(cameraIndex, 'stopped');
        this.loadingCameras.set(cameraIndex, false);

        this.snack.open(`Erro ao iniciar câmera ${cameraIndex}: ${mensagensDeErro(err, 'Verifique a conexão')[0]}`, 'Fechar', { duration: 6000 });
      }
    });
  }

  pararCamera(cameraIndex: number) {
    this.loadingCameras.set(cameraIndex, true);
    this.camerasStatus.set(cameraIndex, 'stopping');
    this.cameraService.pararMonitoramento(cameraIndex);

    this.cameraService.pararCamera(cameraIndex).subscribe({
      next: () => this.finalizarParada(cameraIndex),
      error: (err) => {
        this.finalizarParada(cameraIndex);
        this.snack.open(`Câmera ${cameraIndex} parada localmente (erro no servidor: ${mensagensDeErro(err)[0]})`, 'Fechar', { duration: 5000 });
      }
    });
  }

  private finalizarParada(cameraIndex: number) {
    this.camerasAtivas.set(cameraIndex, false);
    this.camerasStatus.set(cameraIndex, 'stopped');
    this.loadingCameras.set(cameraIndex, false);
    this.streamUrls.delete(cameraIndex);
    this.limparPlacasCamera(cameraIndex);
  }

  toggleCamera(cameraIndex: number) {

    // Evitar múltiplos cliques
    if (this.loadingCameras.get(cameraIndex)) {
      return;
    }

    if (this.camerasAtivas.get(cameraIndex)) {
      this.pararCamera(cameraIndex);
    } else {
      this.iniciarCamera(cameraIndex);
    }
  }

  pararTodasCameras() {

    this.cameras.forEach(cam => {
      if (this.camerasAtivas.get(cam) && !this.loadingCameras.get(cam)) {
        this.pararCamera(cam);
      }
    });
  }

  selecionarCamera(cameraIndex: number) {
    if (this.modoExibicao === 'unica') {
      this.cameraSelecionada = cameraIndex;

      // Se a câmera selecionada não está ativa, iniciar automaticamente
      if (!this.camerasAtivas.get(cameraIndex)) {
        this.iniciarCamera(cameraIndex);
      }
    }
  }

  getStreamUrl(cameraIndex: number): string {
    return this.streamUrls.get(cameraIndex) ?? '';
  }

  /** Pede um ticket novo e (re)aponta o <img> do stream. Cada ticket vale uma única conexão. */
  renovarStream(cameraIndex: number) {
    this.cameraService.obterTicketStream().subscribe({
      next: (ticket) => this.streamUrls.set(cameraIndex, this.cameraService.obterStreamUrl(cameraIndex, ticket)),
      error: (err) => this.snack.open(`Não foi possível abrir o vídeo da câmera ${cameraIndex}: ${mensagensDeErro(err)[0]}`, 'Fechar', { duration: 6000 })
    });
  }

  /** O stream caiu (ticket já usado, servidor reiniciou...): tenta reconectar poucas vezes. */
  aoFalharStream(cameraIndex: number) {
    if (!this.camerasAtivas.get(cameraIndex)) {
      return;
    }
    const tentativas = (this.tentativasStream.get(cameraIndex) ?? 0) + 1;
    this.tentativasStream.set(cameraIndex, tentativas);
    if (tentativas <= 3) {
      this.renovarStream(cameraIndex);
    } else {
      this.snack.open(`O vídeo da câmera ${cameraIndex} não carrega. Pare e inicie a câmera novamente.`, 'Fechar', { duration: 6000 });
    }
  }

  aoCarregarStream(cameraIndex: number) {
    this.tentativasStream.set(cameraIndex, 0);
  }

  /** Trocar de modo recria os <img>; como o ticket é de uso único, cada vídeo ativo ganha um novo. */
  trocarModo(modo: 'unica' | 'multiplas') {
    this.modoExibicao = modo;
    this.cameras.filter(cam => this.camerasAtivas.get(cam)).forEach(cam => this.renovarStream(cam));
  }

  getCameraStatus(cameraIndex: number): string {
    return this.camerasStatus.get(cameraIndex) || 'stopped';
  }

  isCameraLoading(cameraIndex: number): boolean {
    return this.loadingCameras.get(cameraIndex) || false;
  }

  getActiveCamerasCount(): number {
    return Array.from(this.camerasAtivas.values()).filter(active => active).length;
  }

  // ========== MÉTODOS DE PLACAS ==========

  iniciarMonitoramentoDetecoes() {
    // A deduplicação por timestamp é feita no CameraService; aqui não há debounce porque ele
    // descartava placas de câmeras diferentes que chegavam juntas.
    const sub = this.cameraService.placasDetectadas$.subscribe(deteccao => this.adicionarPlacaDetectada(deteccao));
    this.subscriptions.push(sub);
  }

  adicionarPlacaDetectada(deteccao: DeteccaoPlaca) {
    const chave = `${deteccao.plate}_${deteccao.camera}`;
    if (this.placasJaProcessadas.has(chave)) {
      return;
    }

    // Verificar se a câmera está ativa
    if (!this.camerasAtivas.get(deteccao.camera)) {
      return;
    }

    this.placasDetectadas.unshift({
      ...deteccao,
      timestamp: deteccao.timestamp * 1000 // o servidor manda segundos; a tela usa milissegundos
    });

    if (this.placasDetectadas.length > 20) {
      this.placasDetectadas.pop();
    }

    this.placasJaProcessadas.add(chave);
    setTimeout(() => {
      this.placasJaProcessadas.delete(chave);
    }, 30000);

    this.buscarAluguelPorPlaca(deteccao.plate);
    this.aplicarFiltros();
  }

  limparPlacasCamera(cameraIndex: number) {
    // Remover apenas placas da câmera especificada
    this.placasDetectadas = this.placasDetectadas.filter(p => p.camera !== cameraIndex);
    this.aplicarFiltros();
  }

  // ========== MÉTODOS DE ALUGUÉIS ==========

  iniciarAtualizacaoAlugueis() {
    const sub = interval(30000).subscribe(() => {
      this.atualizarInformacoesAlugueis();
    });
    this.subscriptions.push(sub);
  }

  atualizarInformacoesAlugueis() {
    this.aluguelService.selecionarEmAberto().subscribe({
      next: (alugueis: SelecionarAlugueisDto[]) => {
        this.placasComAluguel.clear();
        alugueis.forEach((aluguel: SelecionarAlugueisDto) => {
          if (aluguel.automovelPlaca) {
            this.placasComAluguel.set(aluguel.automovelPlaca, aluguel);
          }
        });
        this.aplicarFiltros();
      }
    });
  }

  buscarAluguelPorPlaca(placa: string) {
    this.aluguelService.selecionarEmAberto().subscribe({
      next: (alugueis: SelecionarAlugueisDto[]) => {
        const aluguel = alugueis.find((a: SelecionarAlugueisDto) =>
          a.automovelPlaca && a.automovelPlaca.toUpperCase() === placa.toUpperCase()
        );
        if (aluguel) {
          this.placasComAluguel.set(placa, aluguel);
        }
        this.aplicarFiltros();
      }
    });
  }

  // ========== FILTROS ==========

  aplicarFiltros() {
    let filtradas = [...this.placasDetectadas];

    if (this.filtroPlaca) {
      filtradas = filtradas.filter(p =>
        p.plate.toLowerCase().includes(this.filtroPlaca.toLowerCase())
      );
    }

    if (this.filtroCamera !== null) {
      filtradas = filtradas.filter(p => p.camera === this.filtroCamera);
    }

    if (this.apenasNaoProcessadas) {
      filtradas = filtradas.filter(p => !this.placasComAluguel.has(p.plate));
    }

    this.placasFiltradas = filtradas;
  }

  onFiltroChange() {
    this.aplicarFiltros();
  }

  // ========== UTILITÁRIOS ==========

  formatarData(timestamp: number): string {
    return new Date(timestamp).toLocaleTimeString('pt-BR');
  }

  formatarDataString(dataString?: string): string {
    if (!dataString) return '';
    try {
      const data = new Date(dataString);
      return data.toLocaleDateString('pt-BR');
    } catch {
      return '';
    }
  }

  navegarParaDevolucao(placa: string) {
    const aluguel = this.placasComAluguel.get(placa);
    if (aluguel && aluguel.id) {
      this.router.navigate(['/alugueis', aluguel.id, 'devolver']);
    } else {
      this.aluguelService.selecionarEmAberto().subscribe({
        next: (alugueis: SelecionarAlugueisDto[]) => {
          const aluguelParaPlaca = alugueis.find((a: SelecionarAlugueisDto) =>
            a.automovelPlaca && a.automovelPlaca.toUpperCase() === placa.toUpperCase()
          );
          if (aluguelParaPlaca && aluguelParaPlaca.id) {
            this.router.navigate(['/alugueis', aluguelParaPlaca.id, 'devolver']);
          } else {
            this.snack.open(`Nenhum aluguel em aberto encontrado para a placa ${placa}`, 'Fechar', { duration: 5000 });
          }
        },
        error: () => {
          this.snack.open('Erro ao buscar informações do aluguel', 'Fechar', { duration: 5000 });
        }
      });
    }
  }

  limparPlacas() {
    this.placasDetectadas = [];
    this.placasFiltradas = [];
  }

  temAluguelEmAberto(placa: string): boolean {
    return this.placasComAluguel.has(placa);
  }

  getAluguelInfo(placa: string): SelecionarAlugueisDto | undefined {
    return this.placasComAluguel.get(placa);
  }

}
