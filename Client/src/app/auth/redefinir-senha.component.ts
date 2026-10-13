import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { mensagensDeErro } from '../core/api-error';
import { NotificacaoService } from '../core/notificacao.service';
import { FormFieldComponent } from '../shared/form-fields/form-field.component';
import { MENSAGENS_SENHA, VALIDADORES_SENHA } from '../shared/validators/senha';
import { AuthShellComponent } from './auth-shell.component';
import { AuthService } from './auth.service';

/** Erro `mismatch` no campo de confirmação para a mensagem aparecer embaixo dele. */
function senhasIguais(grupo: AbstractControl): ValidationErrors | null {
  const confirmar = grupo.get('confirmar');
  if (!confirmar) return null;
  const erros = { ...(confirmar.errors ?? {}) };
  if (grupo.get('senha')?.value !== confirmar.value) erros['mismatch'] = true;
  else delete erros['mismatch'];
  confirmar.setErrors(Object.keys(erros).length ? erros : null, { emitEvent: false });
  return null;
}

/**
 * Página aberta pelo link do e-mail de recuperação: `/redefinir-senha#email=...&token=...`. Os dados vão no fragmento
 * (#), que o navegador não envia ao servidor nem a proxies; a página os lê aqui e os usa só neste formulário.
 */
@Component({
  selector: 'app-redefinir-senha',
  imports: [ReactiveFormsModule, RouterLink, NgIcon, HlmButtonImports, HlmInputImports, HlmAlertImports, FormFieldComponent, AuthShellComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-auth-shell titulo="Escolha uma nova senha">
      <header class="mb-6">
        <h2 class="text-headline-s text-foreground">Nova senha</h2>
        <p class="text-body-m text-muted-foreground mt-1">Use ao menos 8 caracteres, com maiúscula, minúscula e número.</p>
      </header>

      @if (!linkValido) {
        <div hlmAlert variant="destructive" role="alert">
          <ng-icon name="lucideCircleAlert" aria-hidden="true" />
          <p hlmAlertDescription>Link incompleto ou inválido. Peça uma nova redefinição de senha.</p>
        </div>
        <p class="text-body-m mt-6 text-center">
          <a routerLink="/esqueci-senha" class="text-primary font-medium underline-offset-4 hover:underline">Pedir um novo link</a>
        </p>
      } @else {
        <form [formGroup]="form" (ngSubmit)="salvar()" class="flex flex-col gap-4" novalidate>
          @if (erro()) {
            <div hlmAlert variant="destructive" role="alert">
              <ng-icon name="lucideCircleAlert" aria-hidden="true" />
              <p hlmAlertDescription>{{ erro() }}</p>
            </div>
          }

          <p class="text-body-m text-muted-foreground">Conta: <strong class="text-foreground">{{ email }}</strong></p>

          <app-form-field label="Nova senha" required [mensagens]="mensagensSenha">
            <input hlmInput type="password" formControlName="senha" autocomplete="new-password" />
          </app-form-field>

          <app-form-field label="Confirmar nova senha" required [mensagens]="mensagensConfirmacao">
            <input hlmInput type="password" formControlName="confirmar" autocomplete="new-password" />
          </app-form-field>

          <button hlmBtn size="lg" type="submit" class="w-full" [disabled]="carregando()" [attr.aria-busy]="carregando()">
            @if (carregando()) {
              <ng-icon name="lucideLoader2" class="animate-spin motion-reduce:animate-none" aria-hidden="true" />
              Salvando…
            } @else {
              Salvar nova senha
            }
          </button>
        </form>
      }
    </app-auth-shell>
  `
})
export class RedefinirSenhaComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notificacao = inject(NotificacaoService);

  readonly email: string;
  private readonly token: string;
  readonly linkValido: boolean;

  readonly form = this.fb.group(
    { senha: ['', [Validators.required, ...VALIDADORES_SENHA]], confirmar: ['', Validators.required] },
    { validators: senhasIguais }
  );
  readonly mensagensSenha = MENSAGENS_SENHA;
  readonly mensagensConfirmacao = { mismatch: 'As senhas não coincidem' };
  readonly carregando = signal(false);
  readonly erro = signal('');

  constructor() {
    const parametros = new URLSearchParams(inject(ActivatedRoute).snapshot.fragment ?? '');
    this.email = parametros.get('email') ?? '';
    this.token = parametros.get('token') ?? '';
    this.linkValido = !!this.email && !!this.token;
  }

  salvar(): void {
    this.form.updateValueAndValidity();
    if (this.form.invalid || this.carregando()) {
      this.form.markAllAsTouched();
      return;
    }

    this.carregando.set(true);
    this.erro.set('');
    const { senha, confirmar } = this.form.getRawValue();

    this.auth.redefinirSenha(this.email, this.token, senha, confirmar).subscribe({
      next: () => {
        this.carregando.set(false);
        this.notificacao.sucesso('Senha redefinida. Entre com a nova senha.');
        void this.router.navigate(['/login']);
      },
      error: err => {
        this.carregando.set(false);
        this.erro.set(mensagensDeErro(err, 'Não foi possível redefinir a senha agora.').at(-1)!);
      }
    });
  }
}
