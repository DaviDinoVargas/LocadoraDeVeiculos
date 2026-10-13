import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { FormFieldComponent } from '../shared/form-fields/form-field.component';
import { AuthShellComponent } from './auth-shell.component';
import { AuthService } from './auth.service';

/**
 * "Esqueci minha senha" (S18). A resposta é a mesma para qualquer e-mail: a tela nunca diz se a conta existe.
 * O link de redefinição chega por e-mail e abre /redefinir-senha.
 */
@Component({
  selector: 'app-esqueci-senha',
  imports: [ReactiveFormsModule, RouterLink, NgIcon, HlmButtonImports, HlmInputImports, HlmAlertImports, FormFieldComponent, AuthShellComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-auth-shell titulo="Recupere o acesso à sua conta">
      <header class="mb-6">
        <h2 class="text-headline-s text-foreground">Esqueci minha senha</h2>
        <p class="text-body-m text-muted-foreground mt-1">Informe o e-mail da conta e enviaremos um link para escolher uma nova senha.</p>
      </header>

      @if (enviado()) {
        <div hlmAlert role="status" aria-live="polite">
          <ng-icon name="lucideMail" aria-hidden="true" />
          <p hlmAlertDescription>
            Se existir uma conta com este e-mail, enviamos as instruções. O link vale por 24 horas. Confira também a caixa de spam.
          </p>
        </div>
        <p class="text-body-m mt-6 text-center">
          <a routerLink="/login" class="text-primary font-medium underline-offset-4 hover:underline">Voltar para o login</a>
        </p>
      } @else {
        <form [formGroup]="form" (ngSubmit)="enviar()" class="flex flex-col gap-4" novalidate>
          @if (erro()) {
            <div hlmAlert variant="destructive">
              <ng-icon name="lucideCircleAlert" aria-hidden="true" />
              <p hlmAlertDescription>{{ erro() }}</p>
            </div>
          }

          <app-form-field label="E-mail" required>
            <input hlmInput type="email" formControlName="email" autocomplete="email" placeholder="usuario@exemplo.com" />
          </app-form-field>

          <button hlmBtn size="lg" type="submit" class="w-full" [disabled]="carregando()" [attr.aria-busy]="carregando()">
            @if (carregando()) {
              <ng-icon name="lucideLoader2" class="animate-spin motion-reduce:animate-none" aria-hidden="true" />
              Enviando…
            } @else {
              Enviar link
            }
          </button>
        </form>

        <p class="text-body-m text-muted-foreground mt-6 text-center">
          Lembrou a senha?
          <a routerLink="/login" class="text-primary font-medium underline-offset-4 hover:underline">Entrar</a>
        </p>
      }
    </app-auth-shell>
  `
})
export class EsqueciSenhaComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);

  readonly form = this.fb.group({ email: ['', [Validators.required, Validators.email]] });
  readonly carregando = signal(false);
  readonly enviado = signal(false);
  readonly erro = signal('');

  enviar(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.carregando.set(true);
    this.erro.set('');

    this.auth.solicitarRecuperacaoDeSenha(this.form.controls.email.value.trim()).subscribe({
      next: () => {
        this.carregando.set(false);
        this.enviado.set(true);
      },
      error: err => {
        this.carregando.set(false);
        this.erro.set(
          err.status === 429
            ? 'Muitas tentativas. Aguarde alguns minutos e tente de novo.'
            : 'Não foi possível enviar agora. Tente novamente em instantes.'
        );
      }
    });
  }
}
