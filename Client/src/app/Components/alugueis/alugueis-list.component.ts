import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { filter, map } from 'rxjs';
import { ConfirmacaoService } from '../../core/confirmacao.service';
import { NotificacaoService } from '../../core/notificacao.service';
import { crudLista } from '../../shared/data-table/crud-lista';
import { DataTableComponent } from '../../shared/data-table/data-table.component';
import { AcaoLinha, ColunaTabela } from '../../shared/data-table/data-table.types';
import { formatarData, formatarMoeda } from '../../shared/data-table/data-table.util';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { DevolucoesService } from '../devolucoes/devolucoes.service';
import { PdfService } from '../pdf/pdf.service';
import { SelecionarAlugueisDto } from './aluguel.model';
import { AlugueisService } from './alugueis.service';
import { mascararPlaca } from '../../shared/mascaras/mascara.directive';

const ABAS: { status: string; rotulo: string }[] = [
  { status: 'todos', rotulo: 'Todos' },
  { status: 'Reservado', rotulo: 'Reservados' },
  { status: 'EmAndamento', rotulo: 'Em andamento' },
  { status: 'Concluido', rotulo: 'Concluídos' },
  { status: 'Cancelado', rotulo: 'Cancelados' },
  { status: 'em-aberto', rotulo: 'Em aberto' }
];

@Component({
  selector: 'app-alugueis-list',
  imports: [RouterLink, NgIcon, HlmButtonImports, PageComponent, PageHeaderComponent, DataTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './alugueis-list.component.html'
})
export class AlugueisListComponent implements OnInit {
  private readonly svc = inject(AlugueisService);
  private readonly devolucoesSvc = inject(DevolucoesService);
  private readonly pdf = inject(PdfService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly confirmacao = inject(ConfirmacaoService);
  private readonly notificacao = inject(NotificacaoService);

  readonly abas = ABAS;
  readonly filtroStatus = signal('todos');

  // O filtro é feito no servidor: todos -> GET /alugueis, em-aberto -> /alugueis/em-aberto e
  // cada status -> /alugueis/status/{status} (antes carregava tudo e filtrava na memória).
  private readonly lista = crudLista<SelecionarAlugueisDto>({
    listar: () => {
      const status = this.filtroStatus();
      return status === 'todos' ? this.svc.listar() : status === 'em-aberto' ? this.svc.selecionarEmAberto() : this.svc.listarPorStatus(status);
    },
    excluir: id => this.svc.excluir(id),
    singular: 'aluguel',
    carregarAoCriar: false
  });
  readonly alugueisFiltrados = this.lista.dados;
  readonly carregando = this.lista.carregando;
  readonly erro = this.lista.erro;
  readonly excluir = this.lista.excluir;

  // Link de fora (sidebar "Em aberto", KPI da Home) muda o ?status= com a tela já aberta.
  private readonly statusNaUrl = toSignal(this.route.queryParamMap.pipe(map(p => p.get('status') ?? 'todos')), { initialValue: 'todos' });

  private readonly nomesDosFiltros: Record<string, string> = {
    todos: 'Todos os aluguéis', 'em-aberto': 'Em aberto', Reservado: 'Reservados',
    EmAndamento: 'Em andamento', Concluido: 'Concluídos', Cancelado: 'Cancelados'
  };

  readonly colunas: ColunaTabela<SelecionarAlugueisDto>[] = [
    { id: 'condutorNome', cabecalho: 'Condutor' },
    { id: 'automovelPlaca', cabecalho: 'Veículo', formatar: a => mascararPlaca(a.automovelPlaca) },
    { id: 'clienteNome', cabecalho: 'Cliente' },
    { id: 'dataSaida', cabecalho: 'Saída', tipo: 'data' },
    { id: 'dataRetornoPrevisto', cabecalho: 'Retorno previsto', tipo: 'data' },
    { id: 'valorPrevisto', cabecalho: 'Valor previsto', tipo: 'moeda' },
    {
      id: 'situacao',
      cabecalho: 'Status',
      tipo: 'enum',
      filtravel: false, // o filtro de status são as abas acima (consultam o servidor)
      valor: a => a.status,
      opcoes: [
        { valor: 'Reservado', rotulo: 'Reservado', tom: 'info' },
        { valor: 'EmAndamento', rotulo: 'Em andamento', tom: 'warning' },
        { valor: 'Concluido', rotulo: 'Concluído', tom: 'success' },
        { valor: 'Cancelado', rotulo: 'Cancelado', tom: 'error' }
      ]
    }
  ];

  readonly acoes = computed<AcaoLinha<SelecionarAlugueisDto>[]>(() => [
    { rotulo: 'Editar', icone: 'lucidePencil', visivel: a => a.status !== 'Concluido', executar: a => this.editar(a.id) },
    { rotulo: 'Iniciar aluguel', icone: 'lucideCircleCheck', visivel: a => a.status === 'Reservado', executar: a => this.iniciar(a.id) },
    { rotulo: 'Registrar devolução', icone: 'lucideUndo2', visivel: a => a.status === 'EmAndamento', executar: a => this.devolver(a.id) },
    { rotulo: 'Ver devolução', icone: 'lucideReceipt', visivel: a => a.status === 'Concluido', executar: a => this.verDevolucao(a.id) },
    { rotulo: 'Cancelar aluguel', icone: 'lucideCircleX', visivel: a => a.status === 'Reservado' || a.status === 'EmAndamento', executar: a => this.cancelar(a.id) },
    { rotulo: 'Excluir', icone: 'lucideTrash2', destrutiva: true, visivel: a => a.status === 'Reservado' || a.status === 'Cancelado', executar: a => this.excluir(a.id) }
  ]);

  readonly tituloLinha = (a: SelecionarAlugueisDto) => `${a.clienteNome} — ${mascararPlaca(a.automovelPlaca)}`;

  constructor() {
    effect(() => {
      const naUrl = this.statusNaUrl();
      untracked(() => {
        if (naUrl !== this.filtroStatus()) this.filtrarPorStatus(naUrl, false);
      });
    });
  }

  ngOnInit(): void {
    const statusInicial = this.route.snapshot.queryParamMap.get('status');
    if (statusInicial) {
      this.filtroStatus.set(statusInicial);
    }
    this.carregar();
  }

  carregar(): void {
    this.lista.carregar();
  }

  filtrarPorStatus(status: string, atualizarUrl = true): void {
    this.filtroStatus.set(status);
    this.carregar();
    if (atualizarUrl) {
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { status: status === 'todos' ? null : status, page: null },
        queryParamsHandling: 'merge',
        replaceUrl: true
      });
    }
  }

  /** Exporta em PDF exatamente a lista que está na tela (já filtrada por status). */
  async exportarPdf(): Promise<void> {
    if (this.alugueisFiltrados().length === 0) return;
    await this.pdf.gerarRelatorioAlugueis(this.alugueisFiltrados(), this.nomesDosFiltros[this.filtroStatus()] ?? this.filtroStatus());
  }

  // Aluguel concluído: leva à devolução registrada (GET /devolucoes/aluguel/{id}).
  verDevolucao(aluguelId?: string): void {
    if (!aluguelId) return;

    this.devolucoesSvc.obterPorAluguelId(aluguelId).subscribe({
      next: d => void this.router.navigate([`/devolucoes/${d.id}`]),
      error: () => this.notificacao.aviso('Este aluguel ainda não tem devolução registrada.')
    });
  }

  getStatusText(status: string): string {
    return this.colunas[6].opcoes?.find(o => o.valor === status)?.rotulo ?? status;
  }

  formatarData(dataString: string): string {
    return formatarData(dataString);
  }

  formatarMoeda(valor: number): string {
    return formatarMoeda(valor);
  }

  novo(): void {
    void this.router.navigate(['/alugueis/new']);
  }

  editar(id?: string): void {
    if (id) void this.router.navigate([`/alugueis/${id}/edit`]);
  }

  devolver(id?: string): void {
    if (id) void this.router.navigate([`/alugueis/${id}/devolver`]);
  }

  iniciar(id?: string): void {
    if (!id) return;

    this.confirmacao
      .confirmar({ titulo: 'Iniciar aluguel', mensagem: 'Deseja realmente iniciar este aluguel?', confirmar: 'Iniciar aluguel' })
      .pipe(filter(Boolean))
      .subscribe(() => {
        this.svc.iniciar(id).subscribe({
          next: () => {
            this.notificacao.sucesso('Aluguel iniciado com sucesso');
            this.carregar();
          }
        });
      });
  }

  cancelar(id?: string): void {
    if (!id) return;

    this.confirmacao
      .confirmar({ titulo: 'Cancelar aluguel', mensagem: 'Deseja realmente cancelar este aluguel?', confirmar: 'Cancelar aluguel', cancelar: 'Voltar', destrutivo: true })
      .pipe(filter(Boolean))
      .subscribe(() => {
        this.svc.cancelar(id).subscribe({
          next: () => {
            this.notificacao.sucesso('Aluguel cancelado com sucesso');
            this.carregar();
          }
        });
      });
  }
}
