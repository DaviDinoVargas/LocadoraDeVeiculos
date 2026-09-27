import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../auth/auth.service';
import { FaceAuthService } from './face-auth.service';
import { WebcamCaptureService } from '../../shared/webcam-capture.service';

type EstadoCamera = 'parada' | 'ligando' | 'ativa' | 'erro';

@Component({
  selector: 'app-verificacao-facial',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './verificacao-facial.component.html',
  styleUrls: ['./verificacao-facial.component.scss']
})
export class VerificacaoFacialComponent implements OnInit, OnDestroy {
  @ViewChild('video') videoRef?: ElementRef<HTMLVideoElement>;
  @ViewChild('canvas') canvasRef?: ElementRef<HTMLCanvasElement>;

  personId = '';
  cadastrado = false;
  estadoCamera: EstadoCamera = 'parada';
  processando = false;
  mensagem = '';
  mensagemTipo: 'sucesso' | 'erro' | 'info' = 'info';

  constructor(
    private auth: AuthService,
    private faceAuth: FaceAuthService,
    private webcam: WebcamCaptureService
  ) {}

  ngOnInit(): void {
    const usuario = this.auth.getUsuario();
    this.personId = usuario ? `usuario:${usuario.email}` : '';
    if (this.personId) {
      this.faceAuth.status(this.personId).subscribe({
        next: (resp) => (this.cadastrado = resp.cadastrado),
        error: () => {}
      });
    }
  }

  ngOnDestroy(): void {
    this.pararCamera();
  }

  async ligarCamera(): Promise<void> {
    if (this.estadoCamera === 'ativa') {
      return;
    }
    this.estadoCamera = 'ligando';
    try {
      if (this.videoRef) {
        await this.webcam.ligar(this.videoRef.nativeElement);
      }
      this.estadoCamera = 'ativa';
    } catch (e) {
      this.estadoCamera = 'erro';
      this.mostrarMensagem('Não foi possível acessar a câmera. Verifique as permissões do navegador.', 'erro');
    }
  }

  pararCamera(): void {
    this.webcam.desligar();
    this.estadoCamera = 'parada';
  }

  private capturarFrameBase64(): string | null {
    if (!this.videoRef || !this.canvasRef) {
      return null;
    }
    return this.webcam.capturarFrameBase64(this.videoRef.nativeElement, this.canvasRef.nativeElement);
  }

  cadastrarRosto(): void {
    if (this.estadoCamera !== 'ativa' || this.processando) {
      return;
    }
    const imagem = this.capturarFrameBase64();
    if (!imagem) {
      return;
    }
    this.processando = true;
    this.auth.cadastrarRosto(imagem).subscribe({
      next: (resp) => {
        this.processando = false;
        this.cadastrado = true;
        this.mostrarMensagem(`Rosto cadastrado com sucesso (${resp.amostras} amostra(s) salvas).`, 'sucesso');
      },
      error: (err) => {
        this.processando = false;
        this.mostrarMensagem(this.mensagemDeErro(err, 'cadastrar'), 'erro');
      }
    });
  }

  verificarIdentidade(): void {
    if (this.estadoCamera !== 'ativa' || this.processando) {
      return;
    }
    const imagem = this.capturarFrameBase64();
    if (!imagem) {
      return;
    }
    this.processando = true;
    this.faceAuth.verificar(this.personId, imagem).subscribe({
      next: (resp) => {
        this.processando = false;
        if (resp.match) {
          this.mostrarMensagem(`Identidade confirmada (confiança ${(resp.confidence * 100).toFixed(0)}%).`, 'sucesso');
        } else {
          this.mostrarMensagem(`Rosto não corresponde ao cadastrado (confiança ${(resp.confidence * 100).toFixed(0)}%).`, 'erro');
        }
      },
      error: (err) => {
        this.processando = false;
        this.mostrarMensagem(this.mensagemDeErro(err, 'verificar'), 'erro');
      }
    });
  }

  private mensagemDeErro(err: any, acao: 'cadastrar' | 'verificar'): string {
    if (err?.status === 422) {
      return 'Nenhum rosto detectado na imagem. Aproxime-se da câmera e tente novamente.';
    }
    if (err?.status === 404 && acao === 'verificar') {
      return 'Nenhum rosto cadastrado ainda. Cadastre o seu rosto primeiro.';
    }
    return `Não foi possível ${acao} o rosto agora. Tente novamente.`;
  }

  private mostrarMensagem(texto: string, tipo: 'sucesso' | 'erro' | 'info'): void {
    this.mensagem = texto;
    this.mensagemTipo = tipo;
  }
}
