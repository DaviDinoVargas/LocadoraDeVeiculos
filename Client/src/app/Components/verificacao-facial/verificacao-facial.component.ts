import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, OnInit, inject, signal, viewChild } from '@angular/core';
import { NgIcon } from '@ng-icons/core';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { filter } from 'rxjs/operators';
import { AuthService } from '../../auth/auth.service';
import { mensagensDeErro } from '../../core/api-error';
import { ConfirmacaoService } from '../../core/confirmacao.service';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { WebcamCaptureService, WebcamErro } from '../../shared/webcam-capture.service';

type EstadoCamera = 'parada' | 'ligando' | 'ativa' | 'erro';
type TipoMensagem = 'sucesso' | 'erro' | 'info';

@Component({
  selector: 'app-verificacao-facial',
  imports: [NgIcon, HlmAlertImports, HlmButtonImports, PageComponent, PageHeaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './verificacao-facial.component.html'
})
export class VerificacaoFacialComponent implements OnInit, OnDestroy {
  private readonly video = viewChild<ElementRef<HTMLVideoElement>>('video');
  private readonly canvas = viewChild<ElementRef<HTMLCanvasElement>>('canvas');

  private readonly confirmacao = inject(ConfirmacaoService);
  private readonly auth = inject(AuthService);
  private readonly webcam = inject(WebcamCaptureService);

  readonly cadastrado = signal(false);
  readonly estadoCamera = signal<EstadoCamera>('parada');
  readonly processando = signal(false);
  readonly mensagem = signal('');
  readonly mensagemTipo = signal<TipoMensagem>('info');
  /** Motivo da falha da câmera, já explicado (permissão, câmera em uso por outro programa, demora...). */
  readonly erroCamera = signal('');

  ngOnInit(): void {
    // O status vem da Web API (que sabe quem é o usuário pelo JWT). O navegador não fala mais
    // com o serviço de ML nem monta o personId: antes isso expunha o ML e quebrava com e-mail
    // em maiúsculas.
    this.auth.statusRosto().subscribe({
      next: resp => this.cadastrado.set(resp.cadastrado),
      error: () => this.mostrarMensagem('Não foi possível consultar o cadastro do rosto agora.', 'erro')
    });
  }

  ngOnDestroy(): void {
    this.pararCamera();
  }

  async ligarCamera(): Promise<void> {
    if (this.estadoCamera() === 'ativa') {
      return;
    }
    this.estadoCamera.set('ligando');
    this.erroCamera.set('');
    try {
      const video = this.video()?.nativeElement;
      if (video) {
        await this.webcam.ligar(video);
      }
      this.estadoCamera.set('ativa');
    } catch (e) {
      this.estadoCamera.set('erro');
      this.erroCamera.set(e instanceof WebcamErro ? e.message : 'Não foi possível acessar a câmera. Verifique as permissões do navegador.');
    }
  }

  pararCamera(): void {
    this.webcam.desligar();
    this.estadoCamera.set('parada');
  }

  private capturarFrameBase64(): string | null {
    const video = this.video()?.nativeElement;
    const canvas = this.canvas()?.nativeElement;
    return video && canvas ? this.webcam.capturarFrameBase64(video, canvas) : null;
  }

  cadastrarRosto(): void {
    if (this.estadoCamera() !== 'ativa' || this.processando()) {
      return;
    }
    const imagem = this.capturarFrameBase64();
    if (!imagem) {
      return;
    }
    this.processando.set(true);
    this.auth.cadastrarRosto(imagem).subscribe({
      next: resp => {
        this.processando.set(false);
        this.cadastrado.set(true);
        this.mostrarMensagem(`Rosto cadastrado com sucesso (${resp.amostras} amostra(s) salvas).`, 'sucesso');
      },
      error: err => {
        this.processando.set(false);
        this.mostrarMensagem(mensagensDeErro(err, 'Não foi possível cadastrar o rosto agora. Tente novamente.')[0], 'erro');
      }
    });
  }

  verificarIdentidade(): void {
    if (this.estadoCamera() !== 'ativa' || this.processando()) {
      return;
    }
    const imagem = this.capturarFrameBase64();
    if (!imagem) {
      return;
    }
    this.processando.set(true);
    this.auth.verificarRosto(imagem).subscribe({
      next: resp => {
        this.processando.set(false);
        const confianca = `${(resp.confianca * 100).toFixed(0)}%`;
        if (resp.confirmado) {
          this.mostrarMensagem(`Identidade confirmada (confiança ${confianca}).`, 'sucesso');
        } else {
          this.mostrarMensagem(`Rosto não corresponde ao cadastrado (confiança ${confianca}).`, 'erro');
        }
      },
      error: err => {
        this.processando.set(false);
        this.mostrarMensagem(mensagensDeErro(err, 'Não foi possível verificar o rosto agora. Tente novamente.')[0], 'erro');
      }
    });
  }

  /** Apaga o rosto cadastrado do usuário logado, depois de confirmar (ação sem volta). */
  apagarRosto(): void {
    if (this.processando() || !this.cadastrado()) {
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
        this.processando.set(true);
        this.auth.removerRosto().subscribe({
          next: () => {
            this.processando.set(false);
            this.cadastrado.set(false);
            this.mostrarMensagem('Seu rosto foi apagado. Para usar o login facial, cadastre-o novamente.', 'sucesso');
          },
          error: err => {
            this.processando.set(false);
            this.mostrarMensagem(mensagensDeErro(err, 'Não foi possível apagar o rosto agora. Tente novamente.')[0], 'erro');
          }
        });
      });
  }

  private mostrarMensagem(texto: string, tipo: TipoMensagem): void {
    this.mensagem.set(texto);
    this.mensagemTipo.set(tipo);
  }
}
