import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AuthService } from './auth.service';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { WebcamCaptureService } from '../shared/webcam-capture.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatIconModule
  ],
  templateUrl: './login.component.html',
  styleUrls: ['./scss/auth-shared-styles.css']
})
export class LoginComponent implements OnInit, OnDestroy {
  @ViewChild('video') videoRef?: ElementRef<HTMLVideoElement>;
  @ViewChild('canvas') canvasRef?: ElementRef<HTMLCanvasElement>;

  form: FormGroup;
  returnUrl: string;
  loading = false;
  showPassword = false;

  modoFacial = false;
  cameraAtiva = false;

  constructor(
    private fb: FormBuilder,
    private auth: AuthService,
    private router: Router,
    private route: ActivatedRoute,
    private snackBar: MatSnackBar,
    private webcam: WebcamCaptureService
  ) {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      senha: ['', Validators.required]
    });

    // Pega a URL de retorno dos query params
    this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/home';
    console.log('LoginComponent inicializado, returnUrl:', this.returnUrl);
  }

  ngOnInit(): void {
    // Verifica se já está logado
    if (this.auth.isLoggedIn()) {
      console.log('Já está logado, redirecionando para:', this.returnUrl);
      this.router.navigateByUrl(this.returnUrl);
    }
  }

  ngOnDestroy(): void {
    this.webcam.desligar();
  }

  toggleShowPassword(): void {
    this.showPassword = !this.showPassword;
  }

  async alternarModoFacial(): Promise<void> {
    this.modoFacial = !this.modoFacial;

    if (!this.modoFacial) {
      this.webcam.desligar();
      this.cameraAtiva = false;
      return;
    }

    if (this.form.get('email')?.invalid) {
      this.snackBar.open('Informe seu e-mail para entrar com reconhecimento facial.', 'Fechar', { duration: 3500 });
      this.modoFacial = false;
      return;
    }

    try {
      if (this.videoRef) {
        await this.webcam.ligar(this.videoRef.nativeElement);
        this.cameraAtiva = true;
      }
    } catch {
      this.snackBar.open('Não foi possível acessar a câmera. Verifique as permissões do navegador.', 'Fechar', { duration: 4000 });
      this.modoFacial = false;
    }
  }

  entrarComRosto(): void {
    const email = this.form.get('email')?.value;
    if (!email || !this.videoRef || !this.canvasRef || this.loading) {
      return;
    }

    const imagem = this.webcam.capturarFrameBase64(this.videoRef.nativeElement, this.canvasRef.nativeElement);
    if (!imagem) {
      return;
    }

    this.loading = true;
    this.auth.entrarComRosto(email, imagem).subscribe({
      next: () => {
        this.loading = false;
        this.webcam.desligar();
        this.snackBar.open('Login facial realizado com sucesso!', 'Fechar', { duration: 2000 });
        this.router.navigateByUrl(this.returnUrl).catch(() => this.router.navigate(['/home']));
      },
      error: (err) => {
        this.loading = false;
        const mensagem = err.status === 422
          ? 'Nenhum rosto detectado. Aproxime-se da câmera e tente novamente.'
          : 'Não foi possível confirmar sua identidade. Tente novamente ou entre com sua senha.';
        this.snackBar.open(mensagem, 'Fechar', { duration: 4500 });
      }
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.snackBar.open('Por favor, preencha o formulário corretamente.', 'Fechar', { duration: 3000 });
      return;
    }

    this.loading = true;
    const { email, senha } = this.form.value;

    this.auth.autenticar(email, senha).subscribe({
      next: () => {
        this.loading = false;

        this.snackBar.open('Login realizado com sucesso!', 'Fechar', {
          duration: 2000,
          horizontalPosition: 'center',
          verticalPosition: 'top'
        });

        // Adiciona um pequeno delay para garantir que o token foi salvo
        setTimeout(() => {
          this.router.navigateByUrl(this.returnUrl).then(success => {
            if (!success) {
              this.router.navigate(['/home']);
            }
          }).catch(() => {
            this.router.navigate(['/home']);
          });
        }, 100);
      },
      error: (err) => {
        this.loading = false;

        let errorMessage = 'Falha na autenticação. Verifique suas credenciais.';

        if (err.status === 401) {
          errorMessage = 'Email ou senha incorretos.';
        } else if (err.status === 0) {
          errorMessage = 'Não foi possível conectar ao servidor. Verifique sua conexão.';
        } else if (err.error?.message) {
          errorMessage = err.error.message;
        }

        this.snackBar.open(errorMessage, 'Fechar', {
          duration: 5000,
          horizontalPosition: 'center',
          verticalPosition: 'top'
        });
      }
    });
  }
}
