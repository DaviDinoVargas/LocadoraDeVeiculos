import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, ParamMap } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AlugueisService } from './alugueis.service';
import { ClientesService } from '../clientes/clientes.service';
import { CondutoresService } from '../condutor/condutores.service';
import { VeiculosService } from '../veiculos/veiculos.service';
import { TaxasServicosService } from '../taxa-servico/taxas-servicos.service';
import { AluguelDto, AluguelCompletoDto, SimulacaoAluguelDto, SimularAluguelRequest } from './aluguel.model';
import { ClienteDto } from '../clientes/cliente.model';
import { CondutorDto } from '../condutor/condutor.model';
import { VeiculoDto } from '../veiculos/veiculo.model';
import { TaxaServicoDto } from '../taxa-servico/taxa-servico.model';
import { Subject, Subscription, merge, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, map, switchMap, tap } from 'rxjs/operators';
import { mensagensDeErro } from '../../core/api-error';
import { CuponsService } from '../cupons/cupons.service';
import { DesafiosCupomService } from '../desafios-cupom/desafios-cupom.service';
import { ProgressoDesafioDto } from '../desafios-cupom/desafio-cupom.model';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-aluguel-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './aluguel-form.component.html',
  styleUrls: ['../scss/global.scss']
})
export class AluguelFormComponent implements OnInit, OnDestroy {
  form!: FormGroup;
  id?: string | null = null;
  loading = false;
  editMode = false;
  serverErrors: string[] = [];

  clientes: ClienteDto[] = [];
  condutoresFiltrados: CondutorDto[] = [];
  veiculos: VeiculoDto[] = [];
  veiculosDisponiveis: VeiculoDto[] = [];
  taxasServicos: TaxaServicoDto[] = [];
  taxasSelecionadas: string[] = [];
  // Caução do aluguel em edição, vinda do servidor. Em um aluguel novo o servidor copia o valor
  // vigente em Configuracao.ValorGarantia ao salvar (antes o front fixava 1000).
  caucao: number | null = null;

  // Simulação do preço (POST /alugueis/simular): o servidor é a única fonte do cálculo.
  simulacao: SimulacaoAluguelDto | null = null;
  simulando = false;
  erroSimulacao = '';
  private readonly taxasAlteradas$ = new Subject<void>();
  private simulacaoSub?: Subscription;

  // Disponibilidade: só oferece veículos sem aluguel ativo no período escolhido (antes listava todos
  // e o conflito só aparecia como 409 depois do envio).
  avisoDisponibilidade = '';

  // Cupom: validado no servidor (POST /cupons/validar) sobre o valor simulado, então o desconto
  // mostrado é o mesmo que será gravado ao salvar.
  cupomStatus: 'vazio' | 'aguardando' | 'validando' | 'valido' | 'invalido' = 'vazio';
  cupomMensagem = '';
  descontoCupom = 0;
  private cupomDoAluguel: string | null = null; // cupom já gravado no aluguel em edição
  private readonly simulacaoAtualizada$ = new Subject<void>();
  private cupomSub?: Subscription;

  // Desafios do cliente selecionado (GET /desafios-cupom/cliente/{id}/progresso)
  progressoDesafios: ProgressoDesafioDto[] = [];
  private desafiosSub?: Subscription;
  private condutoresSub?: Subscription;
  private readonly periodoCarregado$ = new Subject<void>();
  private disponibilidadeSub?: Subscription;

  private sub: Subscription | null = null;

  constructor(
    private fb: FormBuilder,
    private svc: AlugueisService,
    private clientesSvc: ClientesService,
    private condutoresSvc: CondutoresService,
    private veiculosSvc: VeiculosService,
    private taxasSvc: TaxasServicosService,
    private cuponsSvc: CuponsService,
    private desafiosSvc: DesafiosCupomService,
    private route: ActivatedRoute,
    private router: Router,
    private snack: MatSnackBar
  ) {
    this.form = this.fb.group({
      clienteId: ['', Validators.required],
      condutorId: ['', Validators.required],
      automovelId: ['', Validators.required],
      dataSaida: ['', Validators.required],
      dataRetornoPrevisto: ['', Validators.required],
      taxasServicosIds: [[]],
      cupomCodigo: ['']
    });
  }

  ngOnInit() {
    this.carregarDados();
    this.iniciarSimulacao();
    this.iniciarDisponibilidade();
    this.iniciarCupom();
    this.iniciarDesafios();
    this.iniciarCondutores();

    this.sub = this.route.paramMap.subscribe((params: ParamMap) => {
      this.id = params.get('id');
      this.editMode = !!this.id;

      if (this.editMode && this.id) {
        this.carregar(this.id);
      }
    });
  }

  ngOnDestroy() {
    this.sub?.unsubscribe();
    this.simulacaoSub?.unsubscribe();
    this.disponibilidadeSub?.unsubscribe();
    this.cupomSub?.unsubscribe();
    this.desafiosSub?.unsubscribe();
    this.condutoresSub?.unsubscribe();
  }

  carregarDados() {
    this.carregarClientes();
    this.carregarVeiculos();
    this.carregarTaxasServicos();
  }

  carregarClientes() {
    this.clientesSvc.listar().subscribe({
      next: clientes => this.clientes = clientes,
      error: () => this.snack.open('Erro ao carregar clientes', 'Fechar', { duration: 4000 })
    });
  }

  carregarVeiculos() {
    this.veiculosSvc.listar().subscribe({
      next: veiculos => {
        this.veiculos = veiculos;
        // Sem período escolhido não dá para saber quem está livre: mostra todos até as datas serem informadas.
        this.veiculosDisponiveis = veiculos;
      },
      error: () => this.snack.open('Erro ao carregar veículos', 'Fechar', { duration: 4000 })
    });
  }

  carregarTaxasServicos() {
    this.taxasSvc.listar().subscribe({
      next: taxas => this.taxasServicos = taxas,
      error: () => this.snack.open('Erro ao carregar taxas e serviços', 'Fechar', { duration: 4000 })
    });
  }

  // Condutores do cliente escolhido (GET /condutores/cliente/{id}). Antes carregava TODOS os
  // condutores e filtrava na memória, e na edição a lista continuava com todos.
  private iniciarCondutores() {
    this.condutoresSub = this.form.get('clienteId')!.valueChanges.pipe(
      distinctUntilChanged(),
      switchMap(clienteId => clienteId
        ? this.condutoresSvc.listarPorCliente(clienteId).pipe(catchError(() => {
            this.snack.open('Erro ao carregar os condutores do cliente', 'Fechar', { duration: 4000 });
            return of([] as CondutorDto[]);
          }))
        : of([] as CondutorDto[]))
    ).subscribe(condutores => {
      this.condutoresFiltrados = condutores;

      // Condutor escolhido que não pertence ao cliente atual deixa de valer.
      const atual = this.form.get('condutorId')!.value;
      if (atual && !condutores.some(c => c.id === atual)) {
        this.form.patchValue({ condutorId: '' });
      }
    });
  }

  onTaxaChange(event: Event, taxa: TaxaServicoDto) {
    if ((event.target as HTMLInputElement).checked) {
      this.taxasSelecionadas.push(taxa.id!);
    } else {
      const index = this.taxasSelecionadas.indexOf(taxa.id!);
      if (index > -1) {
        this.taxasSelecionadas.splice(index, 1);
      }
    }
    this.form.patchValue({ taxasServicosIds: this.taxasSelecionadas });
    this.taxasAlteradas$.next();
  }

  /** Monta o pedido de simulação ou null enquanto faltam veículo/datas válidas. */
  private montarSimulacao(): SimularAluguelRequest | null {
    const { automovelId, dataSaida, dataRetornoPrevisto } = this.form.getRawValue();
    if (!automovelId || !dataSaida || !dataRetornoPrevisto) return null;
    if (new Date(dataRetornoPrevisto) <= new Date(dataSaida)) return null;
    return { automovelId, dataSaida, dataRetornoPrevisto, taxasServicosIds: [...this.taxasSelecionadas] };
  }

  private iniciarSimulacao() {
    const mudancas$ = merge(
      this.form.get('automovelId')!.valueChanges,
      this.form.get('dataSaida')!.valueChanges,
      this.form.get('dataRetornoPrevisto')!.valueChanges,
      this.taxasAlteradas$
    );

    // switchMap cancela a simulação anterior quando o usuário muda outro campo.
    this.simulacaoSub = mudancas$.pipe(
      debounceTime(300),
      map(() => this.montarSimulacao()),
      tap(pedido => {
        this.erroSimulacao = '';
        this.simulando = pedido !== null;
        if (!pedido) this.simulacao = null;
      }),
      switchMap(pedido => pedido
        ? this.svc.simular(pedido).pipe(
            catchError(err => {
              this.erroSimulacao = mensagensDeErro(err, 'Não foi possível calcular o valor agora.')[0];
              return of(null);
            }))
        : of(null))
    ).subscribe(simulacao => {
      this.simulando = false;
      if (simulacao) this.simulacao = simulacao;
      else if (this.erroSimulacao) this.simulacao = null;
      this.simulacaoAtualizada$.next();
    });
  }

  private periodoEscolhido(): { saida: string; retorno: string } | null {
    const { dataSaida, dataRetornoPrevisto } = this.form.getRawValue();
    if (!dataSaida || !dataRetornoPrevisto) return null;
    if (new Date(dataRetornoPrevisto) <= new Date(dataSaida)) return null;
    return { saida: dataSaida, retorno: dataRetornoPrevisto };
  }

  private iniciarDisponibilidade() {
    const mudancas$ = merge(
      this.form.get('dataSaida')!.valueChanges,
      this.form.get('dataRetornoPrevisto')!.valueChanges,
      this.periodoCarregado$
    );

    this.disponibilidadeSub = mudancas$.pipe(
      debounceTime(300),
      map(() => this.periodoEscolhido()),
      tap(() => (this.avisoDisponibilidade = '')),
      switchMap(periodo => periodo
        ? this.veiculosSvc.listarDisponiveis(periodo.saida, periodo.retorno, this.id ?? undefined)
            .pipe(catchError(() => of(null))) // falhou: volta à lista completa, o servidor ainda valida ao salvar
        : of(null))
    ).subscribe(disponiveis => {
      if (disponiveis === null) {
        this.veiculosDisponiveis = this.veiculos;
        return;
      }

      this.veiculosDisponiveis = disponiveis;
      const escolhido = this.form.get('automovelId')!.value;

      if (escolhido && !disponiveis.some(v => v.id === escolhido)) {
        this.form.patchValue({ automovelId: '' });
        this.avisoDisponibilidade = 'O veículo escolhido não está disponível neste período. Escolha outro.';
      } else if (disponiveis.length === 0) {
        this.avisoDisponibilidade = 'Nenhum veículo disponível neste período.';
      }
    });
  }

  private iniciarCupom() {
    const mudancas$ = merge(this.form.get('cupomCodigo')!.valueChanges, this.simulacaoAtualizada$);

    this.cupomSub = mudancas$.pipe(
      debounceTime(400),
      map(() => (this.form.get('cupomCodigo')!.value ?? '').trim().toUpperCase()),
      tap(codigo => {
        if (!codigo) {
          this.cupomStatus = 'vazio';
          this.cupomMensagem = '';
          this.descontoCupom = 0;
        } else if (this.editMode && codigo === this.cupomDoAluguel) {
          this.cupomStatus = 'valido';
          this.cupomMensagem = 'Cupom já aplicado neste aluguel.';
        } else if (!this.simulacao) {
          this.cupomStatus = 'aguardando';
          this.cupomMensagem = 'Escolha o veículo e as datas para validar o cupom.';
          this.descontoCupom = 0;
        } else {
          this.cupomStatus = 'validando';
          this.cupomMensagem = '';
        }
      }),
      switchMap(codigo => this.cupomStatus === 'validando'
        ? this.cuponsSvc.validar(codigo, this.simulacao!.valorPrevisto).pipe(
            catchError(err => of({
              valido: false,
              motivoInvalido: mensagensDeErro(err, 'Não foi possível validar o cupom agora.')[0],
              valorDesconto: 0,
              valorComDesconto: this.simulacao!.valorPrevisto
            })))
        : of(null))
    ).subscribe(resultado => {
      if (!resultado) return;
      this.cupomStatus = resultado.valido ? 'valido' : 'invalido';
      this.descontoCupom = resultado.valido ? resultado.valorDesconto : 0;
      this.cupomMensagem = resultado.valido
        ? `Desconto de ${this.formatarMoeda(resultado.valorDesconto)} aplicado.`
        : (resultado.motivoInvalido ?? 'Cupom inválido.');
    });
  }

  private iniciarDesafios() {
    this.desafiosSub = this.form.get('clienteId')!.valueChanges.pipe(
      distinctUntilChanged(),
      switchMap(clienteId => clienteId
        ? this.desafiosSvc.progressoDoCliente(clienteId).pipe(catchError(() => of([] as ProgressoDesafioDto[])))
        : of([] as ProgressoDesafioDto[]))
    ).subscribe(progresso => (this.progressoDesafios = progresso));
  }

  /** Aplica no formulário o cupom liberado por um desafio cumprido. */
  usarCupomDoDesafio(codigo: string) {
    this.form.patchValue({ cupomCodigo: codigo });
  }

  percentualDoDesafio(d: ProgressoDesafioDto): number {
    return d.meta > 0 ? Math.min(100, Math.round((d.progresso / d.meta) * 100)) : 0;
  }

  /** Aluguel já contratado mantém a caução de quando foi feito; aluguel novo usa a vigente. */
  caucaoExibida(): number {
    return this.caucao ?? this.simulacao?.valorCaucao ?? 0;
  }

  /** Total a pagar: diárias + taxas − desconto do cupom + caução. */
  totalAPagar(): number {
    if (!this.simulacao) return 0;
    return this.simulacao.valorPrevisto - this.descontoCupom + this.caucaoExibida();
  }

  formatarMoeda(valor: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL'
    }).format(valor);
  }

  carregar(id: string) {
    this.loading = true;
    this.svc.obter(id).subscribe({
      next: (a: AluguelCompletoDto) => {
        // <input type="date"> só aceita yyyy-MM-dd: com o ISO que a API devolve ele aparecia em branco.
        this.form.patchValue({
          ...a,
          dataSaida: String(a.dataSaida ?? '').slice(0, 10),
          dataRetornoPrevisto: String(a.dataRetornoPrevisto ?? '').slice(0, 10)
        });
        this.taxasSelecionadas = a.taxasServicos?.map(t => t.id) || [];
        this.form.patchValue({ taxasServicosIds: this.taxasSelecionadas }, { emitEvent: false });
        this.descontoCupom = a.valorDesconto || 0;
        this.cupomDoAluguel = a.cupomCodigo ? String(a.cupomCodigo).toUpperCase() : null;
        this.caucao = a.valorCaucao ?? null;
        this.loading = false;
        this.taxasAlteradas$.next(); // calcula o resumo do aluguel carregado
        this.periodoCarregado$.next(); // e a disponibilidade no período já contratado
      },
      error: () => {
        this.loading = false;
        this.snack.open('Erro ao carregar aluguel', 'Fechar', { duration: 4000 });
        this.router.navigate(['/alugueis']);
      }
    });
  }

  salvar() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.snack.open('Verifique os campos obrigatórios', 'Fechar', { duration: 3500 });
      return;
    }

    if (this.cupomStatus === 'invalido' || this.cupomStatus === 'validando') {
      this.snack.open(
        this.cupomStatus === 'validando' ? 'Aguarde a validação do cupom.' : 'Corrija ou remova o cupom antes de salvar.',
        'Fechar', { duration: 3500 });
      return;
    }

    const raw = this.form.getRawValue();
    const payload: AluguelDto = {
      ...raw,
      // O servidor descarta este número e recalcula (CalculoAluguelService); é enviado só porque
      // o contrato da API ainda o carrega.
      valorPrevisto: this.simulacao?.valorPrevisto ?? 0,
      cupomCodigo: raw.cupomCodigo?.trim() || null,
      status: 'Reservado'
    };

    this.loading = true;

    const successHandler = () => {
      this.loading = false;
      this.snack.open(this.editMode ? 'Aluguel atualizado' : 'Aluguel criado', 'Fechar', { duration: 3000 });
      this.router.navigate(['/alugueis']);
    };

    const errorHandler = (err: unknown) => {
      this.loading = false;
      this.serverErrors = mensagensDeErro(err);
      this.snack.open(this.serverErrors[0], 'Fechar', { duration: 4000 });
    };

    if (this.editMode && this.id) {
      this.svc.atualizar(this.id, payload).subscribe({
        next: successHandler,
        error: errorHandler
      });
    } else {
      this.svc.criar(payload).subscribe({
        next: successHandler,
        error: errorHandler
      });
    }
  }

  cancelar() {
    this.router.navigate(['/alugueis']);
  }
}
