import { Component, ElementRef, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { filter } from 'rxjs/operators';
import { AuthService } from '../../auth/auth.service';
import { ConfirmacaoService } from '../../core/confirmacao.service';
import { mensagensDeErro } from '../../core/api-error';
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

  private readonly confirmacao = inject(ConfirmacaoService);

  cadastrado = false;
  estadoCamera: EstadoCamera = 'parada';
  processando = false;
  mensagem = '';
  mensagemTipo: 'sucesso' | 'erro' | 'info' = 'info';

  constructor(
    private auth: AuthService,
    private webcam: WebcamCaptureService
  ) {}

  ngOnInit(): void {
    // O status vem da Web API (que sabe quem é o usuário pelo JWT). O navegador não fala mais
    // com o serviço de ML nem monta o personId: antes isso expunha o ML e quebrava com e-mail
    // em maiúsculas.
    this.auth.statusRosto().subscribe({
      next: (resp) => (this.cadastrado = resp.cadastrado),
      error: () => this.mostrarMensagem('Não foi possível consultar o cadastro do rosto agora.', 'erro')
    });
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
        this.mostrarMensagem(mensagensDeErro(err, 'Não foi possível cadastrar o rosto agora. Tente novamente.')[0], 'erro');
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
    this.auth.verificarRosto(imagem).subscribe({
      next: (resp) => {
        this.processando = false;
        const confianca = `${(resp.confianca * 100).toFixed(0)}%`;
        if (resp.confirmado) {
          this.mostrarMensagem(`Identidade confirmada (confiança ${confianca}).`, 'sucesso');
        } else {
          this.mostrarMensagem(`Rosto não corresponde ao cadastrado (confiança ${confianca}).`, 'erro');
        }
      },
      error: (err) => {
        this.processando = false;
        this.mostrarMensagem(mensagensDeErro(err, 'Não foi possível verificar o rosto agora. Tente novamente.')[0], 'erro');
      }
    });
  }

  /** Apaga o rosto cadastrado do usuário logado, depois de confirmar (ação sem volta). */
  apagarRosto(): void {
    if (this.processando || !this.cadastrado) {
      return;
    }
    this.confirmacao
      .confirmar({
        titulo: 'Apagar meu rosto',
        mensagem: 'O rosto cadastrado será apagado do sistema e você deixará de poder entrar com reconhecimento facial até cadastrá-lo de novo.',
        confirmar: 'Apagar rosto',
        destrutivo: true
      })
      .pipe(filter(Boolean))
      .subscribe(() => {
        this.processando = true;
        this.auth.removerRosto().subscribe({
          next: () => {
            this.processando = false;
            this.cadastrado = false;
            this.mostrarMensagem('Seu rosto foi apagado. Para usar o login facial, cadastre-o novamente.', 'sucesso');
          },
          error: (err) => {
            this.processando = false;
            this.mostrarMensagem(mensagensDeErro(err, 'Não foi possível apagar o rosto agora. Tente novamente.')[0], 'erro');
          }
        });
      });
  }

  private mostrarMensagem(texto: string, tipo: 'sucesso' | 'erro' | 'info'): void {
    this.mensagem = texto;
    this.mensagemTipo = tipo;
  }
}
