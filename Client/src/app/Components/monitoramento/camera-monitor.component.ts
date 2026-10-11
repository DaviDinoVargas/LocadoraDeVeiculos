import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { Router } from '@angular/router';
import { CameraService, DeteccaoPlaca } from './camera.service';
import { AlugueisService } from '../alugueis/alugueis.service';
import { SelecionarAlugueisDto } from '../alugueis/aluguel.model';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription, interval, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, catchError } from 'rxjs/operators';
import { MatSnackBar } from '@angular/material/snack-bar';

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

  // Estado
  loading = false;
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

    // Inicializar status das câmeras
    this.cameras.forEach(cam => {
      this.camerasAtivas.set(cam, false);
      this.camerasStatus.set(cam, 'stopped');
      this.loadingCameras.set(cam, false);
    });
  }

  ngOnDestroy() {
    this.subscriptions.forEach(sub => sub.unsubscribe());
    this.pararTodasCameras();
  }

  // ========== MÉTODOS DE CÂMERAS ==========

  carregarCameras() {
    this.loading = true;
    this.cameraService.listarCameras().subscribe({
      next: (response) => {
        // O serviço de ML responde { cameras: number[], count: number } (era tipado como number[] e
        // o componente aceitava até 4 formatos, 3 deles inexistentes).
        const camerasArray = (response?.cameras ?? []).map(Number);

        this.cameras = camerasArray;

        // Inicializar estados das câmeras
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
        console.error('Erro ao carregar câmeras:', err);
        this.loading = false;
        // Fallback seguro para desenvolvimento
        this.cameras = [0, 1, 2, 3];

        // Inicializar estados
        this.cameras.forEach(cam => {
          this.camerasAtivas.set(cam, false);
          this.camerasStatus.set(cam, 'stopped');
          this.loadingCameras.set(cam, false);
        });
      }
    });
  }

  iniciarCamera(cameraIndex: number) {

    // Atualizar estado
    this.loadingCameras.set(cameraIndex, true);
    this.camerasStatus.set(cameraIndex, 'starting');

    this.cameraService.iniciarCamera(cameraIndex).subscribe({
      next: (response) => {

        // Atualizar estado para ativa
        this.camerasAtivas.set(cameraIndex, true);
        this.camerasStatus.set(cameraIndex, 'running');
        this.loadingCameras.set(cameraIndex, false);

        // Iniciar monitoramento para esta câmera
        this.cameraService.iniciarMonitoramento(cameraIndex);

        // Atualizar contador de placas para esta câmera
        this.subscriptions.push(
          this.cameraService.placasDetectadas$.subscribe({
            next: (deteccao) => {
              if (deteccao.camera === cameraIndex) {
                this.adicionarPlacaDetectada(deteccao);
              }
            }
          })
        );
      },
      error: (err) => {
        console.error(`Erro ao iniciar câmera ${cameraIndex}:`, err);

        // Reverter estado
        this.camerasAtivas.set(cameraIndex, false);
        this.camerasStatus.set(cameraIndex, 'stopped');
        this.loadingCameras.set(cameraIndex, false);

        this.snack.open(`Erro ao iniciar câmera ${cameraIndex}: ${err.message || 'Verifique a conexão'}`, 'Fechar', { duration: 5000 });
      }
    });
  }

  pararCamera(cameraIndex: number) {

    // Atualizar estado
    this.loadingCameras.set(cameraIndex, true);
    this.camerasStatus.set(cameraIndex, 'stopping');

    this.cameraService.pararCamera(cameraIndex).subscribe({
      next: (response) => {

        // Atualizar estado para inativa
        this.camerasAtivas.set(cameraIndex, false);
        this.camerasStatus.set(cameraIndex, 'stopped');
        this.loadingCameras.set(cameraIndex, false);

        // Limpar placas específicas desta câmera
        this.limparPlacasCamera(cameraIndex);
      },
      error: (err) => {
        console.error(`Erro ao parar câmera ${cameraIndex}:`, err);

        // Mesmo com erro, marcar como parada
        this.camerasAtivas.set(cameraIndex, false);
        this.camerasStatus.set(cameraIndex, 'stopped');
        this.loadingCameras.set(cameraIndex, false);

        // Forçar parada local mesmo se o servidor falhar
        this.snack.open(`Câmera ${cameraIndex} parada localmente (erro no servidor: ${err.message})`, 'Fechar', { duration: 5000 });
      }
    });
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
    return this.cameraService.obterStreamUrl(cameraIndex);
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
    const sub = this.cameraService.placasDetectadas$
      .pipe(
        debounceTime(500),
        distinctUntilChanged((a, b) => a.plate === b.plate &&
          Math.abs(a.timestamp - b.timestamp) < 2000)
      )
      .subscribe({
        next: (deteccao) => {
          this.adicionarPlacaDetectada(deteccao);
        }
      });
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
      timestamp: Date.now()
    });

    if (this.placasDetectadas.length > 20) {
      this.placasDetectadas.pop();
    }

    this.placasJaProcessadas.add(chave);
    setTimeout(() => {
      this.placasJaProcessadas.delete(chave);
    }, 30000);

    this.buscarAluguelPorPlaca(deteccao.plate);
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
