import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmCheckboxImports } from '@spartan-ng/helm/checkbox';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { Subject, merge, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, map, switchMap, tap } from 'rxjs/operators';
import { mensagensDeErro } from '../../core/api-error';
import { FormProtegido } from '../../core/form-guard';
import { NotificacaoService } from '../../core/notificacao.service';
import { formatarMoeda } from '../../shared/data-table/data-table.util';
import { focarPrimeiroInvalido } from '../../shared/form-fields/crud-form';
import { FormActionsComponent } from '../../shared/form-fields/form-actions.component';
import { FormErrorsComponent } from '../../shared/form-fields/form-errors.component';
import { FormFieldComponent } from '../../shared/form-fields/form-field.component';
import { FormSectionComponent } from '../../shared/form-fields/form-section.component';
import { FormSkeletonComponent } from '../../shared/form-fields/form-skeleton.component';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { mascararCpf } from '../../shared/mascaras/mascara.directive';
import { ClienteDto } from '../clientes/cliente.model';
import { ClientesService } from '../clientes/clientes.service';
import { CondutorDto } from '../condutor/condutor.model';
import { CondutoresService } from '../condutor/condutores.service';
import { CuponsService } from '../cupons/cupons.service';
import { ProgressoDesafioDto } from '../desafios-cupom/desafio-cupom.model';
import { DesafiosCupomService } from '../desafios-cupom/desafios-cupom.service';
import { TaxaServicoDto } from '../taxa-servico/taxa-servico.model';
import { TaxasServicosService } from '../taxa-servico/taxas-servicos.service';
import { VeiculoDto } from '../veiculos/veiculo.model';
import { VeiculosService } from '../veiculos/veiculos.service';
import { AluguelCompletoDto, AluguelDto, SimulacaoAluguelDto, SimularAluguelRequest } from './aluguel.model';
import { AlugueisService } from './alugueis.service';
import { PlacaPipe } from '../../shared/mascaras/placa.pipe';

type StatusCupom = 'vazio' | 'aguardando' | 'validando' | 'valido' | 'invalido';

@Component({
  selector: 'app-aluguel-form',
  imports: [
    ReactiveFormsModule, NgIcon, HlmButtonImports, HlmCheckboxImports, HlmInputImports, HlmLabel, HlmNativeSelectImports, PageComponent,
    PageHeaderComponent, FormSectionComponent, FormFieldComponent, FormActionsComponent, FormErrorsComponent, FormSkeletonComponent, PlacaPipe
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './aluguel-form.component.html'
})
export class AluguelFormComponent implements OnInit, FormProtegido {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly svc = inject(AlugueisService);
  private readonly clientesSvc = inject(ClientesService);
  private readonly condutoresSvc = inject(CondutoresService);
  private readonly veiculosSvc = inject(VeiculosService);
  private readonly taxasSvc = inject(TaxasServicosService);
  private readonly cuponsSvc = inject(CuponsService);
  private readonly desafiosSvc = inject(DesafiosCupomService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notificacao = inject(NotificacaoService);
  private readonly destroyRef = inject(DestroyRef);

  readonly form = this.fb.group({
    clienteId: ['', Validators.required],
    condutorId: ['', Validators.required],
    automovelId: ['', Validators.required],
    dataSaida: ['', Validators.required],
    dataRetornoPrevisto: ['', Validators.required],
    taxasServicosIds: [[] as string[]],
    cupomCodigo: ['']
  });

  readonly id = signal<string | null>(null);
  readonly editMode = computed(() => this.id() !== null);
  readonly loading = signal(false);
  readonly serverErrors = signal<string[]>([]);

  readonly clientes = signal<ClienteDto[]>([]);
  readonly condutoresFiltrados = signal<CondutorDto[]>([]);
  readonly veiculos = signal<VeiculoDto[]>([]);
  readonly veiculosDisponiveis = signal<VeiculoDto[]>([]);
  readonly taxasServicos = signal<TaxaServicoDto[]>([]);
  readonly taxasSelecionadas = signal<string[]>([]);
  // Caução do aluguel em edição, vinda do servidor. Em um aluguel novo o servidor copia o valor
  // vigente em Configuracao.ValorGarantia ao salvar (antes o front fixava 1000).
  readonly caucao = signal<number | null>(null);

  // Simulação do preço (POST /alugueis/simular): o servidor é a única fonte do cálculo.
  readonly simulacao = signal<SimulacaoAluguelDto | null>(null);
  readonly simulando = signal(false);
  readonly erroSimulacao = signal('');
  private readonly taxasAlteradas$ = new Subject<void>();

  // Disponibilidade: só oferece veículos sem aluguel ativo no período escolhido (antes listava todos
  // e o conflito só aparecia como 409 depois do envio).
  readonly avisoDisponibilidade = signal('');

  // Cupom: validado no servidor (POST /cupons/validar) sobre o valor simulado, então o desconto
  // mostrado é o mesmo que será gravado ao salvar.
  readonly cupomStatus = signal<StatusCupom>('vazio');
  readonly cupomMensagem = signal('');
  readonly descontoCupom = signal(0);
  private cupomDoAluguel: string | null = null; // cupom já gravado no aluguel em edição
  private readonly simulacaoAtualizada$ = new Subject<void>();

  // Desafios do cliente selecionado (GET /desafios-cupom/cliente/{id}/progresso)
  readonly progressoDesafios = signal<ProgressoDesafioDto[]>([]);
  private readonly periodoCarregado$ = new Subject<void>();

  /** Texto da primeira opção do condutor, que depende de o cliente já ter sido escolhido. */
  readonly clienteEscolhido = signal(false);

  ngOnInit(): void {
    this.carregarDados();
    this.iniciarSimulacao();
    this.iniciarDisponibilidade();
    this.iniciarCupom();
    this.iniciarDesafios();
    this.iniciarCondutores();

    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const atual = params.get('id');
      this.id.set(atual);
      if (atual) {
        this.carregar(atual);
      }
    });
  }

  carregarDados(): void {
    this.clientesSvc.listar().subscribe({
      next: clientes => this.clientes.set(clientes),
      error: () => this.notificacao.erro('Erro ao carregar clientes')
    });
    this.veiculosSvc.listar().subscribe({
      next: veiculos => {
        this.veiculos.set(veiculos);
        // Sem período escolhido não dá para saber quem está livre: mostra todos até as datas serem informadas.
        this.veiculosDisponiveis.set(veiculos);
      },
      error: () => this.notificacao.erro('Erro ao carregar veículos')
    });
    this.taxasSvc.listar().subscribe({
      next: taxas => this.taxasServicos.set(taxas),
      error: () => this.notificacao.erro('Erro ao carregar taxas e serviços')
    });
  }

  // Condutores do cliente escolhido (GET /condutores/cliente/{id}). Antes carregava TODOS os
  // condutores e filtrava na memória, e na edição a lista continuava com todos.
  private iniciarCondutores(): void {
    this.form.controls.clienteId.valueChanges
      .pipe(
        distinctUntilChanged(),
        tap(clienteId => this.clienteEscolhido.set(!!clienteId)),
        switchMap(clienteId =>
          clienteId
            ? this.condutoresSvc.listarPorCliente(clienteId).pipe(
                catchError(() => {
                  this.notificacao.erro('Erro ao carregar os condutores do cliente');
                  return of([] as CondutorDto[]);
                })
              )
            : of([] as CondutorDto[])
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(condutores => {
        this.condutoresFiltrados.set(condutores);

        // Condutor escolhido que não pertence ao cliente atual deixa de valer.
        const atual = this.form.controls.condutorId.value;
        if (atual && !condutores.some(c => c.id === atual)) {
          this.form.patchValue({ condutorId: '' });
        }
      });
  }

  onTaxaChange(marcada: boolean, taxa: TaxaServicoDto): void {
    this.taxasSelecionadas.update(atuais => {
      const semEla = atuais.filter(id => id !== taxa.id);
      return marcada ? [...semEla, taxa.id!] : semEla;
    });
    this.form.patchValue({ taxasServicosIds: this.taxasSelecionadas() });
    this.taxasAlteradas$.next();
  }

  /** Monta o pedido de simulação ou null enquanto faltam veículo/datas válidos. */
  private montarSimulacao(): SimularAluguelRequest | null {
    const { automovelId, dataSaida, dataRetornoPrevisto } = this.form.getRawValue();
    if (!automovelId || !dataSaida || !dataRetornoPrevisto) return null;
    if (new Date(dataRetornoPrevisto) <= new Date(dataSaida)) return null;
    return { automovelId, dataSaida, dataRetornoPrevisto, taxasServicosIds: [...this.taxasSelecionadas()] };
  }

  private iniciarSimulacao(): void {
    const mudancas$ = merge(
      this.form.controls.automovelId.valueChanges,
      this.form.controls.dataSaida.valueChanges,
      this.form.controls.dataRetornoPrevisto.valueChanges,
      this.taxasAlteradas$
    );

    // switchMap cancela a simulação anterior quando o usuário muda outro campo.
    mudancas$
      .pipe(
        debounceTime(300),
        map(() => this.montarSimulacao()),
        tap(pedido => {
          this.erroSimulacao.set('');
          this.simulando.set(pedido !== null);
          if (!pedido) this.simulacao.set(null);
        }),
        switchMap(pedido =>
          pedido
            ? this.svc.simular(pedido).pipe(
                catchError(err => {
                  this.erroSimulacao.set(mensagensDeErro(err, 'Não foi possível calcular o valor agora.')[0]);
                  return of(null);
                })
              )
            : of(null)
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(simulacao => {
        this.simulando.set(false);
        if (simulacao) this.simulacao.set(simulacao);
        else if (this.erroSimulacao()) this.simulacao.set(null);
        this.simulacaoAtualizada$.next();
      });
  }

  private periodoEscolhido(): { saida: string; retorno: string } | null {
    const { dataSaida, dataRetornoPrevisto } = this.form.getRawValue();
    if (!dataSaida || !dataRetornoPrevisto) return null;
    if (new Date(dataRetornoPrevisto) <= new Date(dataSaida)) return null;
    return { saida: dataSaida, retorno: dataRetornoPrevisto };
  }

  private iniciarDisponibilidade(): void {
    const mudancas$ = merge(this.form.controls.dataSaida.valueChanges, this.form.controls.dataRetornoPrevisto.valueChanges, this.periodoCarregado$);

    mudancas$
      .pipe(
        debounceTime(300),
        map(() => this.periodoEscolhido()),
        tap(() => this.avisoDisponibilidade.set('')),
        switchMap(periodo =>
          periodo
            ? this.veiculosSvc
                .listarDisponiveis(periodo.saida, periodo.retorno, this.id() ?? undefined)
                .pipe(catchError(() => of(null))) // falhou: volta à lista completa, o servidor ainda valida ao salvar
            : of(null)
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(disponiveis => {
        if (disponiveis === null) {
          this.veiculosDisponiveis.set(this.veiculos());
          return;
        }

        this.veiculosDisponiveis.set(disponiveis);
        const escolhido = this.form.controls.automovelId.value;

        if (escolhido && !disponiveis.some(v => v.id === escolhido)) {
          this.form.patchValue({ automovelId: '' });
          this.avisoDisponibilidade.set('O veículo escolhido não está disponível neste período. Escolha outro.');
        } else if (disponiveis.length === 0) {
          this.avisoDisponibilidade.set('Nenhum veículo disponível neste período.');
        }
      });
  }

  private iniciarCupom(): void {
    const mudancas$ = merge(this.form.controls.cupomCodigo.valueChanges, this.simulacaoAtualizada$);

    mudancas$
      .pipe(
        debounceTime(400),
        map(() => (this.form.controls.cupomCodigo.value ?? '').trim().toUpperCase()),
        tap(codigo => {
          if (!codigo) {
            this.cupomStatus.set('vazio');
            this.cupomMensagem.set('');
            this.descontoCupom.set(0);
          } else if (this.editMode() && codigo === this.cupomDoAluguel) {
            this.cupomStatus.set('valido');
            this.cupomMensagem.set('Cupom já aplicado neste aluguel.');
          } else if (!this.simulacao()) {
            this.cupomStatus.set('aguardando');
            this.cupomMensagem.set('Escolha o veículo e as datas para validar o cupom.');
            this.descontoCupom.set(0);
          } else {
            this.cupomStatus.set('validando');
            this.cupomMensagem.set('');
          }
        }),
        switchMap(codigo =>
          this.cupomStatus() === 'validando'
            ? this.cuponsSvc.validar(codigo, this.simulacao()!.valorPrevisto).pipe(
                catchError(err =>
                  of({
                    valido: false,
                    motivoInvalido: mensagensDeErro(err, 'Não foi possível validar o cupom agora.')[0],
                    valorDesconto: 0,
                    valorComDesconto: this.simulacao()!.valorPrevisto
                  })
                )
              )
            : of(null)
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(resultado => {
        if (!resultado) return;
        this.cupomStatus.set(resultado.valido ? 'valido' : 'invalido');
        this.descontoCupom.set(resultado.valido ? resultado.valorDesconto : 0);
        this.cupomMensagem.set(
          resultado.valido ? `Desconto de ${this.formatarMoeda(resultado.valorDesconto)} aplicado.` : (resultado.motivoInvalido ?? 'Cupom inválido.')
        );
      });
  }

  private iniciarDesafios(): void {
    this.form.controls.clienteId.valueChanges
      .pipe(
        distinctUntilChanged(),
        switchMap(clienteId =>
          clienteId ? this.desafiosSvc.progressoDoCliente(clienteId).pipe(catchError(() => of([] as ProgressoDesafioDto[]))) : of([] as ProgressoDesafioDto[])
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(progresso => this.progressoDesafios.set(progresso));
  }

  /** Aplica no formulário o cupom liberado por um desafio cumprido. */
  usarCupomDoDesafio(codigo: string): void {
    this.form.patchValue({ cupomCodigo: codigo });
    this.form.controls.cupomCodigo.markAsDirty();
  }

  percentualDoDesafio(d: ProgressoDesafioDto): number {
    return d.meta > 0 ? Math.min(100, Math.round((d.progresso / d.meta) * 100)) : 0;
  }

  /** Aluguel já contratado mantém a caução de quando foi feito; aluguel novo usa a vigente. */
  caucaoExibida(): number {
    return this.caucao() ?? this.simulacao()?.valorCaucao ?? 0;
  }

  /** Total a pagar: diárias + taxas − desconto do cupom + caução. */
  totalAPagar(): number {
    const simulacao = this.simulacao();
    return simulacao ? simulacao.valorPrevisto - this.descontoCupom() + this.caucaoExibida() : 0;
  }

  formatarMoeda(valor: number): string {
    return formatarMoeda(valor);
  }

  formatarCpf(cpf: string): string {
    return mascararCpf(cpf);
  }

  carregar(id: string): void {
    this.loading.set(true);
    this.svc.obter(id).subscribe({
      next: (a: AluguelCompletoDto) => {
        // <input type="date"> só aceita yyyy-MM-dd: com o ISO que a API devolve ele aparecia em branco.
        this.form.patchValue({
          clienteId: a.clienteId,
          condutorId: a.condutorId,
          automovelId: a.automovelId,
          dataSaida: String(a.dataSaida ?? '').slice(0, 10),
          dataRetornoPrevisto: String(a.dataRetornoPrevisto ?? '').slice(0, 10),
          cupomCodigo: a.cupomCodigo ?? ''
        });
        this.taxasSelecionadas.set(a.taxasServicos?.map(t => t.id) || []);
        this.form.patchValue({ taxasServicosIds: this.taxasSelecionadas() }, { emitEvent: false });
        this.descontoCupom.set(a.valorDesconto || 0);
        this.cupomDoAluguel = a.cupomCodigo ? String(a.cupomCodigo).toUpperCase() : null;
        this.caucao.set(a.valorCaucao ?? null);
        this.form.markAsPristine();
        this.loading.set(false);
        this.taxasAlteradas$.next(); // calcula o resumo do aluguel carregado
        this.periodoCarregado$.next(); // e a disponibilidade no período já contratado
      },
      error: () => {
        this.loading.set(false);
        void this.router.navigate(['/alugueis']);
      }
    });
  }

  salvar(): void {
    this.serverErrors.set([]);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.notificacao.aviso('Verifique os campos destacados.');
      queueMicrotask(focarPrimeiroInvalido);
      return;
    }

    if (this.cupomStatus() === 'invalido' || this.cupomStatus() === 'validando') {
      this.notificacao.aviso(this.cupomStatus() === 'validando' ? 'Aguarde a validação do cupom.' : 'Corrija ou remova o cupom antes de salvar.');
      return;
    }

    const raw = this.form.getRawValue();
    const payload: AluguelDto = {
      ...raw,
      // O servidor descarta este número e recalcula (CalculoAluguelService); é enviado só porque
      // o contrato da API ainda o carrega.
      valorPrevisto: this.simulacao()?.valorPrevisto ?? 0,
      cupomCodigo: raw.cupomCodigo?.trim() || null,
      status: 'Reservado'
    };

    this.loading.set(true);
    const id = this.id();
    const chamada = id ? this.svc.atualizar(id, payload) : this.svc.criar(payload);
    chamada.subscribe({
      next: () => {
        this.loading.set(false);
        this.form.markAsPristine();
        this.notificacao.sucesso(this.editMode() ? 'Aluguel atualizado' : 'Aluguel criado');
        void this.router.navigate(['/alugueis']);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.serverErrors.set(mensagensDeErro(err));
      }
    });
  }

  cancelar(): void {
    void this.router.navigate(['/alugueis']);
  }
}
