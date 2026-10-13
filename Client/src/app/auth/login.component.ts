import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, OnInit, inject, signal, viewChild } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { NotificacaoService } from '../core/notificacao.service';
import { FormFieldComponent } from '../shared/form-fields/form-field.component';
import { WebcamCaptureService, WebcamErro } from '../shared/webcam-capture.service';
import { AuthShellComponent } from './auth-shell.component';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    NgIcon,
    HlmButtonImports,
    HlmInputImports,
    HlmAlertImports,
    FormFieldComponent,
    AuthShellComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.component.html'
})
export class LoginComponent implements OnInit, OnDestroy {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly notificacao = inject(NotificacaoService);
  private readonly webcam = inject(WebcamCaptureService);

  private readonly video = viewChild<ElementRef<HTMLVideoElement>>('video');
  private readonly canvas = viewChild<ElementRef<HTMLCanvasElement>>('canvas');

  readonly form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    senha: ['', Validators.required],
    codigo: ['']
  });

  /** A senha (ou o rosto) estava certa e a conta tem 2FA: o servidor respondeu 428 e a tela pede o código do aplicativo. */
  readonly pedirCodigo = signal(false);

  readonly returnUrl: string = this.route.snapshot.queryParams['returnUrl'] || '/home';
  readonly carregando = signal(false);
  readonly mostrarSenha = signal(false);
  /** Mensagem do último erro de entrada (fica na tela, ligada ao formulário; nunca revela se o e-mail existe). */
  readonly erro = signal('');

  readonly modoFacial = signal(false);
  readonly cameraAtiva = signal(false);

  ngOnInit(): void {
    if (this.auth.isLoggedIn()) {
      void this.router.navigateByUrl(this.returnUrl);
    }
  }

  ngOnDestroy(): void {
    this.webcam.desligar();
  }

  alternarSenha(): void {
    this.mostrarSenha.update(v => !v);
  }

  async alternarModoFacial(): Promise<void> {
    this.erro.set('');

    if (this.modoFacial()) {
      this.webcam.desligar();
      this.cameraAtiva.set(false);
      this.modoFacial.set(false);
      return;
    }

    if (this.form.controls.email.invalid) {
      this.form.controls.email.markAsTouched();
      this.erro.set('Informe seu e-mail para entrar com reconhecimento facial.');
      return;
    }

    this.modoFacial.set(true);
    const video = this.video()?.nativeElement;
    if (!video) return;

    try {
      await this.webcam.ligar(video);
      this.cameraAtiva.set(true);
    } catch (e) {
      this.erro.set(e instanceof WebcamErro ? e.message : 'Não foi possível acessar a câmera. Verifique as permissões do navegador.');
      this.modoFacial.set(false);
    }
  }

  entrarComRosto(): void {
    const video = this.video()?.nativeElement;
    const canvas = this.canvas()?.nativeElement;
    const email = this.form.controls.email.value;
    if (!email || !video || !canvas || this.carregando()) return;

    const imagem = this.webcam.capturarFrameBase64(video, canvas);
    if (!imagem) return;

    this.carregando.set(true);
    this.erro.set('');
    const codigo = this.form.controls.codigo.value.trim();
    this.auth.entrarComRosto(email, imagem, codigo || undefined).subscribe({
      next: () => {
        this.carregando.set(false);
        this.webcam.desligar();
        this.notificacao.sucesso('Login facial realizado com sucesso');
        this.irParaDestino();
      },
      error: err => {
        this.carregando.set(false);
        if (err.status === 428) {
          this.pedirCodigo.set(true);
          this.erro.set('Sua conta usa verificação em duas etapas: informe o código do aplicativo autenticador e capture de novo.');
          return;
        }
        this.erro.set(
          err.status === 422
            ? 'Nenhum rosto detectado. Aproxime-se da câmera e tente novamente.'
            : 'Não foi possível confirmar sua identidade. Tente novamente ou entre com sua senha.'
        );
      }
    });
  }

  entrar(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.carregando.set(true);
    this.erro.set('');
    const { email, senha, codigo } = this.form.getRawValue();

    this.auth.autenticar(email, senha, codigo.trim() || undefined).subscribe({
      next: () => {
        this.carregando.set(false);
        this.notificacao.sucesso('Login realizado com sucesso');
        this.irParaDestino();
      },
      error: err => {
        this.carregando.set(false);
        if (err.status === 428) {
          // Senha certa, conta com 2FA: pede o código sem dizer nada além disso.
          this.pedirCodigo.set(true);
          this.erro.set('Informe o código do aplicativo autenticador (ou um código de recuperação).');
        } else if (err.status === 401) {
          this.erro.set('E-mail ou senha incorretos.');
        } else if (err.status === 0) {
          this.erro.set('Não foi possível conectar ao servidor. Verifique sua conexão.');
        } else {
          this.erro.set(err.error?.message ?? 'Falha na autenticação. Verifique suas credenciais.');
        }
      }
    });
  }

  private irParaDestino(): void {
    this.router.navigateByUrl(this.returnUrl).then(
      ok => ok || this.router.navigate(['/home']),
      () => this.router.navigate(['/home'])
    );
  }
}
