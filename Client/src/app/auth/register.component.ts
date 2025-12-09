// register.component.ts
import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
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
  selector: 'app-register',
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
  templateUrl: './register.component.html',
  styleUrls: ['./scss/auth-shared-styles.css']
})
export class RegisterComponent {
  form: FormGroup;
  loading = false;
  showPassword = false;
  showConfirmPassword = false;

  constructor(
    private fb: FormBuilder,
    private auth: AuthService,
    private router: Router,
    private snackBar: MatSnackBar
  ) {
    this.form = this.fb.group({
      nomeCompleto: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      senha: ['', Validators.required],
      confirmarSenha: ['', Validators.required]
    }, { validators: this.passwordMatchValidator });
  }

  passwordMatchValidator(form: FormGroup) {
    const senha = form.get('senha')?.value;
    const confirmarSenha = form.get('confirmarSenha')?.value;
    return senha === confirmarSenha ? null : { mismatch: true };
  }

  toggleShowPassword(): void {
    this.showPassword = !this.showPassword;
  }

  toggleShowConfirmPassword(): void {
    this.showConfirmPassword = !this.showConfirmPassword;
  }

  submit(): void {
    if (this.form.invalid) {
      this.snackBar.open('Por favor, preencha o formulário corretamente.', 'Fechar', { duration: 3000 });
      return;
    }

    this.loading = true;
    const { nomeCompleto, email, senha, confirmarSenha } = this.form.value;

    this.auth.registrar(nomeCompleto, email, senha, confirmarSenha).subscribe({
      next: () => {
        this.loading = false;
        this.snackBar.open('Conta criada com sucesso!', 'Fechar', { duration: 3000 });
        this.router.navigate(['/home']);
      },
      error: (err) => {
        this.loading = false;
        this.snackBar.open('Falha ao registrar. Tente novamente.', 'Fechar', { duration: 5000 });
      }
    });
  }
}
