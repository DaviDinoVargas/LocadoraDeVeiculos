import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { mensagensDeErro } from '../core/api-error';
import { NotificacaoService } from '../core/notificacao.service';
import { FormFieldComponent } from '../shared/form-fields/form-field.component';
import { AuthShellComponent } from './auth-shell.component';
import { AuthService } from './auth.service';

/** Erro `mismatch` no campo de confirmação (e não no grupo) para a mensagem aparecer embaixo dele. */
function senhasIguais(grupo: AbstractControl): ValidationErrors | null {
  const confirmar = grupo.get('confirmarSenha');
  if (!confirmar) return null;
  const diferente = grupo.get('senha')?.value !== confirmar.value;
  const erros = { ...(confirmar.errors ?? {}) };
  if (diferente) {
    erros['mismatch'] = true;
  } else {
    delete erros['mismatch'];
  }
  confirmar.setErrors(Object.keys(erros).length ? erros : null, { emitEvent: false });
  return null;
}

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, NgIcon, HlmButtonImports, HlmInputImports, HlmAlertImports, FormFieldComponent, AuthShellComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './register.component.html'
})
export class RegisterComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notificacao = inject(NotificacaoService);

  readonly form = this.fb.group(
    {
      nomeCompleto: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      senha: ['', Validators.required],
      confirmarSenha: ['', Validators.required]
    },
    { validators: senhasIguais }
  );

  readonly mensagensConfirmacao = { mismatch: 'As senhas não coincidem' };
  readonly carregando = signal(false);
  readonly mostrarSenha = signal(false);
  readonly mostrarConfirmacao = signal(false);
  readonly erro = signal('');

  alternarSenha(): void {
    this.mostrarSenha.update(v => !v);
  }

  alternarConfirmacao(): void {
    this.mostrarConfirmacao.update(v => !v);
  }

  registrar(): void {
    this.form.updateValueAndValidity();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.carregando.set(true);
    this.erro.set('');
    const { nomeCompleto, email, senha, confirmarSenha } = this.form.getRawValue();

    this.auth.registrar(nomeCompleto, email, senha, confirmarSenha).subscribe({
      next: () => {
        this.carregando.set(false);
        this.notificacao.sucesso('Conta criada com sucesso');
        void this.router.navigate(['/home']);
      },
      error: err => {
        this.carregando.set(false);
        this.erro.set(mensagensDeErro(err, 'Falha ao registrar. Tente novamente.').join(' '));
      }
    });
  }
}
