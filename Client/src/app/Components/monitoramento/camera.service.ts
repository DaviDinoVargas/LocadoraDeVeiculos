import { Injectable, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject, Subscription, EMPTY, of, timer } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';

export interface DeteccaoPlaca {
  camera: number;
  plate: string;
  bbox: [number, number, number, number];
  /** Segundos desde a época (relógio do servidor de ML). */
  timestamp: number;
  confidence?: number;
}

export interface CameraInfo {
  index: number;
  isRunning: boolean;
  fps: number;
}

const INTERVALO_POLLING_MS = 1500;

@Injectable({
  providedIn: 'root'
})
export class CameraService implements OnDestroy {
  // Todas as chamadas passam pelo proxy (ng serve / nginx) e levam o JWT do usuário: o serviço
  // de ML recusa quem não estiver logado.
  private baseUrl = '/api/cameras';
  private placasDetectadasSubject = new Subject<DeteccaoPlaca>();

  placasDetectadas$ = this.placasDetectadasSubject.asObservable();

  // câmera monitorada -> maior timestamp do servidor já entregue. O servidor marca o tempo em
  // segundos; comparar com Date.now() (ms) fazia a deduplicação nunca funcionar.
  private vistoAte = new Map<number, number>();
  private polling?: Subscription;

  constructor(private http: HttpClient) {}

  ngOnDestroy(): void {
    this.pararTodoMonitoramento();
  }

  // Listar câmeras disponíveis
  listarCameras(): Observable<{ cameras: number[]; count: number }> {
    return this.http.get<{ cameras: number[]; count: number }>(`${this.baseUrl}/cameras`);
  }

  // Iniciar câmera
  iniciarCamera(cameraIndex: number, fps: number = 10): Observable<{ ok: boolean; camera: number; fps: number }> {
    return this.http.post<{ ok: boolean; camera: number; fps: number }>(`${this.baseUrl}/start`, { camera: cameraIndex, fps });
  }

  // Parar câmera
  pararCamera(cameraIndex: number): Observable<{ ok: boolean; camera: number }> {
    return this.http.post<{ ok: boolean; camera: number }>(`${this.baseUrl}/stop/${cameraIndex}`, {});
  }

  // Obter últimas detecções (da mais nova para a mais antiga). `desde` filtra no servidor.
  obterUltimasDetecoes(desde = 0): Observable<DeteccaoPlaca[]> {
    return this.http
      .get<{ detections: DeteccaoPlaca[] }>(`${this.baseUrl}/last`, { params: { since: desde } })
      .pipe(map(resposta => resposta.detections ?? []));
  }

  /**
   * Começa a acompanhar as placas de uma câmera. Há UM único temporizador para todas as
   * câmeras (antes cada chamada criava um setInterval que nunca era limpo) e as detecções que já
   * existiam no servidor antes do início não são reemitidas.
   */
  iniciarMonitoramento(cameraIndex: number): void {
    if (this.vistoAte.has(cameraIndex)) {
      return;
    }
    // Reserva o lugar já (evita iniciar duas vezes) e descobre o "marco zero" da câmera.
    this.vistoAte.set(cameraIndex, Number.MAX_SAFE_INTEGER);
    this.obterUltimasDetecoes().pipe(catchError(() => of<DeteccaoPlaca[]>([]))).subscribe(detecoes => {
      if (!this.vistoAte.has(cameraIndex)) {
        return; // parou enquanto a consulta estava em andamento
      }
      const ultimas = detecoes.filter(d => d.camera === cameraIndex).map(d => d.timestamp);
      this.vistoAte.set(cameraIndex, ultimas.length ? Math.max(...ultimas) : 0);
      this.garantirPolling();
    });
  }

  pararMonitoramento(cameraIndex: number): void {
    this.vistoAte.delete(cameraIndex);
    if (this.vistoAte.size === 0) {
      this.polling?.unsubscribe();
      this.polling = undefined;
    }
  }

  pararTodoMonitoramento(): void {
    this.vistoAte.clear();
    this.polling?.unsubscribe();
    this.polling = undefined;
  }

  private garantirPolling(): void {
    if (this.polling) {
      return;
    }
    this.polling = timer(INTERVALO_POLLING_MS, INTERVALO_POLLING_MS)
      .pipe(
        // switchMap cancela a consulta anterior se o servidor demorar; erro de rede não encerra o polling.
        switchMap(() => this.obterUltimasDetecoes(this.menorMarco()).pipe(catchError(() => EMPTY)))
      )
      .subscribe(detecoes => this.emitirNovas(detecoes));
  }

  private menorMarco(): number {
    const marcos = Array.from(this.vistoAte.values()).filter(m => m !== Number.MAX_SAFE_INTEGER);
    return marcos.length ? Math.min(...marcos) : 0;
  }

  private emitirNovas(detecoes: DeteccaoPlaca[]): void {
    // o servidor devolve da mais nova para a mais antiga; emite na ordem cronológica
    [...detecoes].reverse().forEach(d => {
      const marco = this.vistoAte.get(d.camera);
      if (marco === undefined || marco === Number.MAX_SAFE_INTEGER || d.timestamp <= marco) {
        return;
      }
      this.vistoAte.set(d.camera, d.timestamp);
      this.placasDetectadasSubject.next(d);
    });
  }

  /**
   * O <img> do stream MJPEG não consegue mandar o cabeçalho Authorization. Troca-se o JWT por um
   * ticket de uso único e vida curta (30 s) que vai na URL do stream.
   */
  obterTicketStream(): Observable<string> {
    return this.http.post<{ ticket: string }>(`${this.baseUrl}/stream-ticket`, {}).pipe(map(r => r.ticket));
  }

  /** Caminho relativo: passa pelo proxy (ng serve) / nginx, igual ao resto da API (antes era http://localhost:8000). */
  obterStreamUrl(cameraIndex: number, ticket: string): string {
    return `/stream/${cameraIndex}?ticket=${encodeURIComponent(ticket)}`;
  }
}
