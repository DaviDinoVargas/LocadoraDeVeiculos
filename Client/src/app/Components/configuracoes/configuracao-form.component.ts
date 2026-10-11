import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { Observable, concat, last, of } from 'rxjs';
import { mensagensDeErro } from '../../core/api-error';
import { FormProtegido } from '../../core/form-guard';
import { NotificacaoService } from '../../core/notificacao.service';
import { focarPrimeiroInvalido } from '../../shared/form-fields/crud-form';
import { FormActionsComponent } from '../../shared/form-fields/form-actions.component';
import { FormErrorsComponent } from '../../shared/form-fields/form-errors.component';
import { FormFieldComponent } from '../../shared/form-fields/form-field.component';
import { InputPrefixoComponent } from '../../shared/form-fields/input-prefixo.component';
import { FormSectionComponent } from '../../shared/form-fields/form-section.component';
import { FormSkeletonComponent } from '../../shared/form-fields/form-skeleton.component';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { ConfiguracaoDto } from './configuracao.model';
import { ConfiguracoesService } from './configuracoes.service';

@Component({
  selector: 'app-configuracao-form',
  imports: [
    ReactiveFormsModule, NgIcon, HlmInputImports, HlmAlertImports, PageComponent, PageHeaderComponent, FormSectionComponent,
    FormFieldComponent, InputPrefixoComponent, FormActionsComponent, FormErrorsComponent, FormSkeletonComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './configuracao-form.component.html'
})
export class ConfiguracaoFormComponent implements OnInit, FormProtegido {
  private readonly svc = inject(ConfiguracoesService);
  private readonly router = inject(Router);
  private readonly notificacao = inject(NotificacaoService);

  readonly form = inject(NonNullableFormBuilder).group({
    precoCombustivel: [0, [Validators.required, Validators.min(0.01)]],
    valorGarantia: [0, [Validators.required, Validators.min(0), Validators.max(100000)]]
  });

  readonly mensagensCombustivel = { min: 'O preço do combustível deve ser maior que 0.' };
  readonly mensagensGarantia = { min: 'Informe um valor entre R$ 0,00 e R$ 100.000,00.', max: 'Informe um valor entre R$ 0,00 e R$ 100.000,00.' };

  readonly carregando = signal(false);
  readonly salvando = signal(false);
  readonly erros = signal<string[]>([]);
  readonly mensagemSucesso = signal('');
  // Valores carregados do servidor: só chamamos o PUT do que realmente mudou.
  private original: ConfiguracaoDto | null = null;

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    this.carregando.set(true);
    this.svc.obter().subscribe({
      next: config => {
        this.original = config;
        this.form.patchValue(config);
        this.form.markAsPristine();
        this.carregando.set(false);
      },
      error: () => this.carregando.set(false)
    });
  }

  salvar(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.notificacao.aviso('Verifique os campos destacados.');
      queueMicrotask(focarPrimeiroInvalido);
      return;
    }

    const { precoCombustivel, valorGarantia } = this.form.getRawValue();
    this.salvando.set(true);
    this.erros.set([]);
    this.mensagemSucesso.set('');

    // Em sequência (e não em paralelo): os dois PUTs leem e gravam a mesma linha de
    // configuração, e se ela ainda não existir os dois tentariam criá-la.
    const chamadas: Observable<unknown>[] = [];
    if (precoCombustivel !== this.original?.precoCombustivel) {
      chamadas.push(this.svc.atualizarPrecoCombustivel(precoCombustivel));
    }
    if (valorGarantia !== this.original?.valorGarantia) {
      chamadas.push(this.svc.atualizarValorGarantia(valorGarantia));
    }
    const salvar$ = chamadas.length ? concat(...chamadas).pipe(last()) : of(null);

    salvar$.subscribe({
      next: () => {
        this.original = { ...this.original, precoCombustivel, valorGarantia };
        this.salvando.set(false);
        this.form.markAsPristine();
        this.mensagemSucesso.set('Configurações atualizadas com sucesso!');
        this.notificacao.sucesso('Configurações atualizadas');
      },
      error: (err: unknown) => {
        this.salvando.set(false);
        this.erros.set(mensagensDeErro(err));
      }
    });
  }

  cancelar(): void {
    void this.router.navigate(['/home']);
  }
}
