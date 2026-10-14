import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { startWith } from 'rxjs';
import { mensagensDeErro } from '../../core/api-error';
import { FormProtegido } from '../../core/form-guard';
import { NotificacaoService } from '../../core/notificacao.service';
import { formatarData, formatarMoeda } from '../../shared/data-table/data-table.util';
import { focarPrimeiroInvalido } from '../../shared/form-fields/crud-form';
import { FormActionsComponent } from '../../shared/form-fields/form-actions.component';
import { FormErrorsComponent } from '../../shared/form-fields/form-errors.component';
import { FormFieldComponent } from '../../shared/form-fields/form-field.component';
import { FormSectionComponent } from '../../shared/form-fields/form-section.component';
import { FormSkeletonComponent } from '../../shared/form-fields/form-skeleton.component';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { AluguelCompletoDto, SelecionarAlugueisDto, TaxaServicoAluguelDto } from '../alugueis/aluguel.model';
import { AlugueisService } from '../alugueis/alugueis.service';
import { ConfiguracaoDto } from '../configuracoes/configuracao.model';
import { ConfiguracoesService } from '../configuracoes/configuracoes.service';
import { DevolucaoDto, NIVEL_COMBUSTIVEL_OPTIONS } from './devolucao.model';
import { DevolucoesService } from './devolucoes.service';
import { PlacaPipe } from '../../shared/mascaras/placa.pipe';

// Litros que o tanque tem quando está "cheio" para o cálculo do adicional de combustível.
const TANQUE_REFERENCIA_LITROS = 50;
const PERCENTUAL_POR_NIVEL: Record<string, number> = { Cheio: 100, TresQuartos: 75, Metade: 50, UmQuarto: 25, Vazio: 0 };

@Component({
  selector: 'app-devolucao-form',
  imports: [
    ReactiveFormsModule, HlmInputImports, HlmNativeSelectImports, PageComponent, PageHeaderComponent, FormSectionComponent,
    FormFieldComponent, FormActionsComponent, FormErrorsComponent, FormSkeletonComponent, PlacaPipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './devolucao-form.component.html'
})
export class DevolucaoFormComponent implements OnInit, FormProtegido {
  private readonly svc = inject(DevolucoesService);
  private readonly aluguelSvc = inject(AlugueisService);
  private readonly configSvc = inject(ConfiguracoesService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notificacao = inject(NotificacaoService);

  readonly form = inject(NonNullableFormBuilder).group({
    dataDevolucao: ['', [Validators.required]],
    quilometragemFinal: [0, [Validators.required, Validators.min(0)]],
    nivelCombustivel: ['', Validators.required],
    combustivelNoTanque: [0, [Validators.required, Validators.min(0)]]
  });

  readonly aluguelId = signal<string | null>(null);
  readonly carregandoAluguel = signal(false);
  readonly salvando = signal(false);
  readonly erros = signal<string[]>([]);
  readonly aluguel = signal<AluguelCompletoDto | undefined>(undefined);
  readonly configuracao = signal<ConfiguracaoDto | undefined>(undefined);

  readonly dataAtual = new Date().toISOString().split('T')[0];
  readonly nivelCombustivelOptions = NIVEL_COMBUSTIVEL_OPTIONS;
  readonly mensagensData = { required: 'Informe a data da devolução.' };
  readonly mensagensKm = { min: 'A quilometragem deve ser maior ou igual a 0.' };
  readonly mensagensTanque = { min: 'O combustível deve ser maior ou igual a 0.' };

  private readonly valores = toSignal(this.form.valueChanges.pipe(startWith(this.form.getRawValue())), {
    initialValue: this.form.getRawValue()
  });

  readonly valorTaxas = computed(
    () => this.aluguel()?.taxasServicos?.reduce((total: number, taxa: TaxaServicoAluguelDto) => total + taxa.preco, 0) ?? 0
  );

  readonly multa = computed(() => {
    const aluguel = this.aluguel();
    const dataDevolucao = this.valores().dataDevolucao;
    if (!aluguel || !dataDevolucao) return 0;
    return new Date(dataDevolucao) > new Date(aluguel.dataRetornoPrevisto) ? aluguel.valorPrevisto * 0.1 : 0;
  });

  readonly valorCombustivel = computed(() => {
    const config = this.configuracao();
    const { nivelCombustivel, combustivelNoTanque } = this.valores();
    if (!this.aluguel() || !config || !combustivelNoTanque) return 0;

    const esperado = TANQUE_REFERENCIA_LITROS * ((PERCENTUAL_POR_NIVEL[nivelCombustivel ?? ''] ?? 100) / 100);
    return combustivelNoTanque < esperado ? (esperado - combustivelNoTanque) * (config.precoCombustivel || 5.0) : 0;
  });

  readonly valorTotal = computed(() => {
    const aluguel = this.aluguel();
    return aluguel ? aluguel.valorPrevisto + this.valorTaxas() + this.multa() + this.valorCombustivel() : 0;
  });

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      const placa = params['placa'];
      if (placa) {
        this.buscarAluguelPorPlaca(placa);
      }
    });

    const id = this.route.snapshot.paramMap.get('id');
    this.aluguelId.set(id);
    if (id) {
      this.carregarAluguel(id);
      this.carregarConfiguracao();
    }
  }

  buscarAluguelPorPlaca(placa: string): void {
    this.aluguelSvc.selecionarEmAberto().subscribe({
      next: (alugueis: SelecionarAlugueisDto[]) => {
        const aluguel = alugueis.find(a => a.automovelPlaca && a.automovelPlaca.toUpperCase() === placa.toUpperCase());
        if (aluguel) {
          this.aluguelId.set(aluguel.id);
          this.carregarAluguel(aluguel.id);
          this.carregarConfiguracao();
        } else {
          this.notificacao.aviso(`Nenhum aluguel em aberto encontrado para a placa ${placa}`);
        }
      }
    });
  }

  carregarAluguel(id: string): void {
    this.carregandoAluguel.set(true);
    this.aluguelSvc.obter(id).subscribe({
      next: (a: AluguelCompletoDto) => {
        this.aluguel.set(a);
        this.carregandoAluguel.set(false);
      },
      error: () => {
        this.carregandoAluguel.set(false);
        void this.router.navigate(['/alugueis']);
      }
    });
  }

  carregarConfiguracao(): void {
    this.configSvc.obter().subscribe({ next: (config: ConfiguracaoDto) => this.configuracao.set(config) });
  }

  formatarData(dataString: string): string {
    return formatarData(dataString);
  }

  formatarMoeda(valor: number): string {
    return formatarMoeda(valor);
  }

  salvar(): void {
    this.erros.set([]);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.notificacao.aviso('Verifique os campos destacados.');
      queueMicrotask(focarPrimeiroInvalido);
      return;
    }

    const aluguelId = this.aluguelId();
    if (!aluguelId) return;

    const v = this.form.getRawValue();
    const payload: DevolucaoDto = {
      aluguelId,
      dataDevolucao: v.dataDevolucao,
      quilometragemFinal: v.quilometragemFinal,
      nivelCombustivel: v.nivelCombustivel as DevolucaoDto['nivelCombustivel'],
      combustivelNoTanque: v.combustivelNoTanque,
      valorMultas: this.multa(),
      valorAdicionalCombustivel: this.valorCombustivel(),
      valorTotal: this.valorTotal()
    };

    this.salvando.set(true);
    this.svc.registrar(payload).subscribe({
      next: (resp: DevolucaoDto) => {
        this.salvando.set(false);
        this.form.markAsPristine();
        this.notificacao.sucesso('Devolução registrada com sucesso');
        void this.router.navigate(resp.id ? ['/devolucoes', resp.id] : ['/devolucoes']);
      },
      error: (err: unknown) => {
        this.salvando.set(false);
        this.erros.set(mensagensDeErro(err));
      }
    });
  }

  cancelar(): void {
    void this.router.navigate(['/alugueis']);
  }
}
