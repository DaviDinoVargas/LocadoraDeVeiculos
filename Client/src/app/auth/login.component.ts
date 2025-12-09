import { Component, OnInit } from '@angular/core';
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
export class LoginComponent implements OnInit {
  form: FormGroup;
  returnUrl: string;
  loading = false;
  showPassword = false;

  constructor(
    private fb: FormBuilder,
    private auth: AuthService,
    private router: Router,
    private route: ActivatedRoute,
    private snackBar: MatSnackBar
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

  toggleShowPassword(): void {
    this.showPassword = !this.showPassword;
  }

  submit(): void {
    if (this.form.invalid) {
      this.snackBar.open('Por favor, preencha o formulário corretamente.', 'Fechar', { duration: 3000 });
      return;
    }

    this.loading = true;
    const { email, senha } = this.form.value;

    console.log('Tentando login com:', { email });
    console.log('Return URL configurada:', this.returnUrl);

    this.auth.autenticar(email, senha).subscribe({
      next: (token) => {
        this.loading = false;
        console.log('Login bem-sucedido!');
        console.log('Token recebido:', token);
        console.log('Token no localStorage:', this.auth.getAccessToken());
        console.log('isLoggedIn():', this.auth.isLoggedIn());
        console.log('Navegando para:', this.returnUrl);

        this.snackBar.open('Login realizado com sucesso!', 'Fechar', {
          duration: 2000,
          horizontalPosition: 'center',
          verticalPosition: 'top'
        });

        // Adiciona um pequeno delay para garantir que o token foi salvo
        setTimeout(() => {
          this.router.navigateByUrl(this.returnUrl).then(success => {
            console.log('Navegação bem-sucedida?', success);
            if (!success) {
              console.log('Falha na navegação, tentando ir para /home');
              this.router.navigate(['/home']);
            }
          }).catch(err => {
            console.error('Erro na navegação:', err);
            this.router.navigate(['/home']);
          });
        }, 100);
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro no login:', err);

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
