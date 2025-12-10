import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject } from 'rxjs';

export interface DeteccaoPlaca {
  camera: number;
  plate: string;
  bbox: [number, number, number, number];
  timestamp: number;
  confidence?: number;
}

export interface CameraInfo {
  index: number;
  isRunning: boolean;
  fps: number;
}

@Injectable({
  providedIn: 'root'
})
export class CameraService {
  private baseUrl = '/api/cameras'; // URL do servidor Python
  private placasDetectadasSubject = new Subject<DeteccaoPlaca>();

  placasDetectadas$ = this.placasDetectadasSubject.asObservable();

  constructor(private http: HttpClient) {}

  // Listar câmeras disponíveis
  listarCameras(): Observable<number[]> {
    return this.http.get<number[]>(`${this.baseUrl}/cameras`);
  }

  // Iniciar câmera
  iniciarCamera(cameraIndex: number, fps: number = 10): Observable<any> {
    return this.http.post(`${this.baseUrl}/start`, { camera: cameraIndex, fps });
  }

  // Parar câmera
  pararCamera(cameraIndex: number): Observable<any> {
    return this.http.post(`${this.baseUrl}/stop/${cameraIndex}`, {});
  }

  // Obter últimas detecções
  obterUltimasDetecoes(): Observable<DeteccaoPlaca[]> {
    return this.http.get<DeteccaoPlaca[]>(`${this.baseUrl}/last`);
  }

  // Iniciar polling para detecções em tempo real
  iniciarMonitoramento(cameraIndex: number) {
    // Em produção, usar WebSocket para tempo real
    // Por enquanto, usaremos polling
    setInterval(() => {
      this.obterUltimasDetecoes().subscribe({
        next: (detecoes) => {
          detecoes.forEach(d => {
            if (d.camera === cameraIndex) {
              this.placasDetectadasSubject.next(d);
            }
          });
        }
      });
    }, 1000);
  }

  obterStreamUrl(cameraIndex: number): string {
    return `http://localhost:8000/stream/${cameraIndex}`;
  }
}
