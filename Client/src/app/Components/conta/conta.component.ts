import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { AuthService, ConfiguracaoDoisFatores, StatusDoisFatores } from '../../auth/auth.service';
import { mensagensDeErro } from '../../core/api-error';
import { ConfirmacaoService } from '../../core/confirmacao.service';
import { NotificacaoService } from '../../core/notificacao.service';
import { FormFieldComponent } from '../../shared/form-fields/form-field.component';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { MENSAGENS_SENHA, VALIDADORES_SENHA } from '../../shared/validators/senha';

function novaSenhaConfere(grupo: AbstractControl): ValidationErrors | null {
  const confirmar = grupo.get('confirmar');
  if (!confirmar) return null;
  const erros = { ...(confirmar.errors ?? {}) };
  if (grupo.get('nova')?.value !== confirmar.value) erros['mismatch'] = true;
  else delete erros['mismatch'];
  confirmar.setErrors(Object.keys(erros).length ? erros : null, { emitEvent: false });
  return null;
}

/**
 * Minha conta (S1, S18, S27): trocar a senha, ligar/desligar a verificação em duas etapas (aplicativo autenticador) e
 * encerrar a conta da empresa. Trocar a senha e encerrar a conta derrubam todas as sessões: depois disso a tela leva ao login.
 */
@Component({
  selector: 'app-conta',
  imports: [ReactiveFormsModule, NgIcon, HlmAlertImports, HlmButtonImports, HlmInputImports, FormFieldComponent, PageComponent, PageHeaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './conta.component.html'
})
export class ContaComponent implements OnInit {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notificacao = inject(NotificacaoService);
  private readonly confirmacao = inject(ConfirmacaoService);

  readonly ehEmpresa = this.auth.getUsuario()?.cargo === 'Empresa';
  readonly mensagensSenha = MENSAGENS_SENHA;
  readonly mensagensConfirmacao = { mismatch: 'As senhas não coincidem' };

  // ---- trocar senha
  readonly formSenha = this.fb.group(
    { atual: ['', Validators.required], nova: ['', [Validators.required, ...VALIDADORES_SENHA]], confirmar: ['', Validators.required] },
    { validators: novaSenhaConfere }
  );
  readonly salvandoSenha = signal(false);
  readonly erroSenha = signal('');

  // ---- verificação em duas etapas
  readonly status2fa = signal<StatusDoisFatores | null>(null);
  readonly configuracao = signal<ConfiguracaoDoisFatores | null>(null);
  readonly codigosDeRecuperacao = signal<string[]>([]);
  readonly erro2fa = signal('');
  readonly processando2fa = signal(false);
  readonly formAtivar = this.fb.group({ codigo: ['', Validators.required] });
  readonly formDesativar = this.fb.group({ senha: ['', Validators.required], codigo: ['', Validators.required] });

  // ---- encerrar conta
  readonly formEncerrar = this.fb.group({ senha: ['', Validators.required] });
  readonly erroEncerrar = signal('');
  readonly encerrando = signal(false);

  ngOnInit(): void {
    this.carregarStatus();
  }

  private carregarStatus(): void {
    this.auth.statusDoisFatores().subscribe({
      next: status => this.status2fa.set(status),
      error: () => this.erro2fa.set('Não foi possível consultar a verificação em duas etapas agora.')
    });
  }

  private mensagem(err: unknown, padrao: string): string {
    return mensagensDeErro(err, padrao).at(-1)!;
  }

  // ------------------------------------------------------------ senha
  trocarSenha(): void {
    this.formSenha.updateValueAndValidity();
    if (this.formSenha.invalid || this.salvandoSenha()) {
      this.formSenha.markAllAsTouched();
      return;
    }
    this.salvandoSenha.set(true);
    this.erroSenha.set('');
    const { atual, nova, confirmar } = this.formSenha.getRawValue();

    this.auth.alterarSenha(atual, nova, confirmar).subscribe({
      next: () => {
        this.salvandoSenha.set(false);
        this.notificacao.sucesso('Senha alterada. Por segurança, entre novamente.');
        void this.router.navigate(['/login']);
      },
      error: err => {
        this.salvandoSenha.set(false);
        this.erroSenha.set(this.mensagem(err, 'Não foi possível alterar a senha agora.'));
      }
    });
  }

  // ------------------------------------------------------------ 2FA
  configurarDoisFatores(): void {
    this.processando2fa.set(true);
    this.erro2fa.set('');
    this.auth.iniciarDoisFatores().subscribe({
      next: config => {
        this.processando2fa.set(false);
        this.configuracao.set(config);
      },
      error: err => {
        this.processando2fa.set(false);
        this.erro2fa.set(this.mensagem(err, 'Não foi possível iniciar a configuração agora.'));
      }
    });
  }

  ativarDoisFatores(): void {
    if (this.formAtivar.invalid || this.processando2fa()) {
      this.formAtivar.markAllAsTouched();
      return;
    }
    this.processando2fa.set(true);
    this.erro2fa.set('');
    this.auth.ativarDoisFatores(this.formAtivar.controls.codigo.value).subscribe({
      next: resposta => {
        this.processando2fa.set(false);
        this.codigosDeRecuperacao.set(resposta.codigos);
        this.configuracao.set(null);
        this.formAtivar.reset();
        this.carregarStatus();
        this.notificacao.sucesso('Verificação em duas etapas ativada');
      },
      error: err => {
        this.processando2fa.set(false);
        this.erro2fa.set(this.mensagem(err, 'Não foi possível ativar agora.'));
      }
    });
  }

  cancelarConfiguracao(): void {
    this.configuracao.set(null);
    this.formAtivar.reset();
    this.erro2fa.set('');
  }

  desativarDoisFatores(): void {
    if (this.formDesativar.invalid || this.processando2fa()) {
      this.formDesativar.markAllAsTouched();
      return;
    }
    this.processando2fa.set(true);
    this.erro2fa.set('');
    const { senha, codigo } = this.formDesativar.getRawValue();

    this.auth.desativarDoisFatores(senha, codigo).subscribe({
      next: () => {
        this.processando2fa.set(false);
        this.formDesativar.reset();
        this.codigosDeRecuperacao.set([]);
        this.carregarStatus();
        this.notificacao.sucesso('Verificação em duas etapas desativada');
      },
      error: err => {
        this.processando2fa.set(false);
        this.erro2fa.set(this.mensagem(err, 'Não foi possível desativar agora.'));
      }
    });
  }

  async copiarCodigos(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.codigosDeRecuperacao().join('\n'));
      this.notificacao.sucesso('Códigos copiados');
    } catch {
      this.notificacao.aviso('Não foi possível copiar. Selecione os códigos e copie à mão.');
    }
  }

  fecharCodigos(): void {
    this.codigosDeRecuperacao.set([]);
  }

  // ------------------------------------------------------------ encerrar conta
  encerrarConta(): void {
    if (this.formEncerrar.invalid || this.encerrando()) {
      this.formEncerrar.markAllAsTouched();
      return;
    }

    this.confirmacao
      .confirmar({
        titulo: 'Encerrar a conta da empresa?',
        mensagem:
          'A sua conta e as de todos os funcionários serão bloqueadas para sempre, as sessões serão encerradas e os rostos cadastrados serão apagados.',
        confirmar: 'Encerrar conta',
        destrutivo: true
      })
      .subscribe(confirmado => {
        if (!confirmado) return;
        this.encerrando.set(true);
        this.erroEncerrar.set('');
        this.auth.encerrarConta(this.formEncerrar.controls.senha.value).subscribe({
          next: () => {
            this.encerrando.set(false);
            this.notificacao.sucesso('Conta encerrada.');
            void this.router.navigate(['/login']);
          },
          error: err => {
            this.encerrando.set(false);
            this.erroEncerrar.set(this.mensagem(err, 'Não foi possível encerrar a conta agora.'));
          }
        });
      });
  }
}
