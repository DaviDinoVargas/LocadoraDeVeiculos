import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy, Component, Directive, booleanAttribute, ElementRef, HostListener, TemplateRef, computed, contentChildren, effect, inject, input, output,
  signal, untracked, viewChild
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import {
  columnVisibilityFeature, createPaginatedRowModel, createSortedRowModel, injectTable, rowPaginationFeature, rowSortingFeature,
  tableFeatures, type ColumnDef, type PaginationState, type SortingState, type ColumnVisibilityState
} from '@tanstack/angular-table';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmDropdownMenuImports } from '@spartan-ng/helm/dropdown-menu';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { HlmTableImports } from '@spartan-ng/helm/table';
import { EmptyStateComponent } from '../layout/empty-state.component';
import { ErrorStateComponent } from '../layout/error-state.component';
import { StatusBadgeComponent, TomStatus } from '../layout/status-badge.component';
import {
  AcaoLinha, ColunaTabela, ContextoCelula, Densidade, OpcaoColuna, TAMANHOS_PAGINA
} from './data-table.types';
import {
  baixarCsv, compararValores, formatarData, formatarMoeda, formatarNumero, normalizar
} from './data-table.util';

/** `<ng-template appCelula="status" let-linha>…</ng-template>`: célula customizada de uma coluna. */
@Directive({ selector: 'ng-template[appCelula]' })
export class CelulaDirective {
  readonly id = input.required<string>({ alias: 'appCelula' });
  readonly template = inject<TemplateRef<ContextoCelula<unknown>>>(TemplateRef);
}

const features = tableFeatures({
  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  rowPaginationFeature,
  paginatedRowModel: createPaginatedRowModel(),
  columnVisibilityFeature
});

/** Sobe quando o formato salvo mudar: preferências de outra versão são descartadas em vez de interpretadas errado. */
const VERSAO_PREFERENCIAS = 1;

interface Preferencias {
  versao?: number;
  ocultas?: string[];
  densidade?: Densidade;
  tamanho?: number;
}

const CHAVES_RESERVADAS = new Set(['q', 'sort', 'dir', 'page', 'size']);
const ALTURA_LINHA: Record<Densidade, string> = { compacta: 'py-1.5', padrao: 'py-3', confortavel: 'py-4' };
const ROTULO_DENSIDADE: Record<Densidade, string> = { compacta: 'Compacta', padrao: 'Padrão', confortavel: 'Confortável' };

/**
 * Tabela de dados compartilhada (SKILL §16): TODA listagem usa este componente. Entrega busca com
 * debounce (atalho `/`, `Esc` limpa), filtros por coluna com chips removíveis, ordenação, colunas
 * visíveis, densidade, exportação CSV do conjunto filtrado, contador, paginação (10/25/50/100), estados
 * (carregando/vazio/sem resultado/erro) e, no mobile, lista de cartões. Página, ordenação, busca e filtros
 * vivem nos query params da URL (compartilhável; o botão voltar funciona); colunas, densidade e tamanho
 * da página são lembrados por `tableId`.
 *
 * Os dados chegam completos da API (até ~1.000 linhas), então filtrar/ordenar/paginar é no cliente (§16.5).
 * Ordenação e paginação usam o TanStack Table; a busca e os filtros são do componente.
 */
@Component({
  selector: 'app-data-table',
  imports: [
    NgTemplateOutlet, NgIcon, HlmButtonImports, HlmDropdownMenuImports, HlmInputImports, HlmNativeSelectImports,
    HlmSkeleton, HlmTableImports, EmptyStateComponent, ErrorStateComponent, StatusBadgeComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  templateUrl: './data-table.component.html'
})
export class DataTableComponent<T extends object> {
  readonly tableId = input.required<string>();
  readonly colunas = input.required<ColunaTabela<T>[]>();
  readonly dados = input.required<T[]>();
  readonly carregando = input(false);
  readonly erro = input(false);
  readonly mensagemErro = input('Algo deu errado ao buscar os dados. Tente novamente em instantes.');
  /** Nome do que a tabela lista, no plural e no singular ("veículos"/"veículo"): mensagens e arquivo CSV. */
  readonly itens = input('registros');
  readonly item = input('registro');
  readonly acoesLinha = input<AcaoLinha<T>[]>([]);
  readonly linhaId = input<(linha: T) => string>(linha => String((linha as { id?: unknown }).id ?? ''));
  /** Nome da linha para o rótulo acessível do menu de ações ("Ações para Fiat Uno"). */
  readonly tituloLinha = input<(linha: T) => string>(() => '');
  readonly linhasClicaveis = input(false, { transform: booleanAttribute });
  readonly tituloVazio = input('Nada cadastrado ainda');
  readonly descricaoVazio = input('');
  readonly ordenacaoPadrao = input<{ id: string; desc: boolean } | null>(null);

  readonly linhaClique = output<T>();
  readonly recarregar = output<void>();

  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly campoBusca = viewChild<ElementRef<HTMLInputElement>>('campoBusca');
  private readonly celulas = contentChildren(CelulaDirective);

  protected readonly tamanhosPagina = TAMANHOS_PAGINA;
  protected readonly rotuloDensidade = ROTULO_DENSIDADE;
  protected readonly densidades = Object.keys(ROTULO_DENSIDADE) as Densidade[];

  // ---------- estado vindo da URL ----------
  private readonly params = toSignal(this.route.queryParamMap, { initialValue: this.route.snapshot.queryParamMap });
  protected readonly busca = computed(() => this.params().get('q') ?? '');
  private readonly ordenacaoUrl = computed<SortingState>(() => {
    const id = this.params().get('sort');
    if (id && this.colunas().some(c => c.id === id)) return [{ id, desc: this.params().get('dir') === 'desc' }];
    const padrao = this.ordenacaoPadrao();
    return id === null && padrao ? [{ id: padrao.id, desc: padrao.desc }] : [];
  });

  // ---------- preferências por tableId ----------
  private readonly prefsLocais = signal<Preferencias | null>(null);
  private readonly prefs = computed<Preferencias>(() => this.prefsLocais() ?? this.lerPreferencias());
  protected readonly densidade = computed<Densidade>(() => this.prefs().densidade ?? 'padrao');
  protected readonly tamanhoPagina = computed(() => {
    const urlTam = Number(this.params().get('size'));
    if ((TAMANHOS_PAGINA as readonly number[]).includes(urlTam)) return urlTam;
    const salvo = this.prefs().tamanho;
    return salvo && (TAMANHOS_PAGINA as readonly number[]).includes(salvo) ? salvo : 25;
  });

  protected readonly visibilidade = computed<ColumnVisibilityState>(() => {
    const ocultas = this.prefs().ocultas ?? this.colunas().filter(c => c.ocultaPorPadrao).map(c => c.id);
    return Object.fromEntries(this.colunas().map(c => [c.id, !ocultas.includes(c.id)]));
  });

  // ---------- filtros ----------
  protected readonly colunasFiltraveis = computed(() =>
    this.colunas().filter(c => (c.filtravel ?? (c.tipo === 'enum' || c.tipo === 'booleano')) && !CHAVES_RESERVADAS.has(c.id))
  );

  protected opcoesDe(coluna: ColunaTabela<T>): OpcaoColuna[] {
    if (coluna.opcoes?.length) return coluna.opcoes;
    if (coluna.tipo === 'booleano') {
      return [{ valor: 'true', rotulo: 'Sim' }, { valor: 'false', rotulo: 'Não' }];
    }
    const distintos = new Set(this.dados().map(l => this.chaveFiltro(coluna, l)).filter(v => v !== ''));
    return [...distintos].sort((a, b) => compararValores(a, b)).slice(0, 30).map(v => ({ valor: v, rotulo: v }));
  }

  private readonly filtros = computed<Record<string, string[]>>(() => {
    const resultado: Record<string, string[]> = {};
    for (const coluna of this.colunasFiltraveis()) {
      const bruto = this.params().get(coluna.id);
      if (bruto) resultado[coluna.id] = bruto.split(',').filter(Boolean);
    }
    return resultado;
  });

  protected readonly chips = computed(() =>
    this.colunasFiltraveis().flatMap(coluna =>
      (this.filtros()[coluna.id] ?? []).map(valor => ({
        colunaId: coluna.id,
        valor,
        texto: `${coluna.cabecalho}: ${this.opcoesDe(coluna).find(o => o.valor === valor)?.rotulo ?? valor}`
      }))
    )
  );

  protected readonly filtrosAtivos = computed(() => this.chips().length + (this.busca() ? 1 : 0));

  private readonly dadosFiltrados = computed(() => {
    const termo = normalizar(this.busca());
    const filtros = this.filtros();
    const colunasFiltro = this.colunasFiltraveis();
    return this.dados().filter(linha => {
      for (const coluna of colunasFiltro) {
        const selecionados = filtros[coluna.id];
        if (selecionados?.length && !selecionados.includes(this.chaveFiltro(coluna, linha))) return false;
      }
      if (!termo) return true;
      return this.colunas().some(c => normalizar(`${this.valorBruto(c, linha)} ${this.texto(c, linha)}`).includes(termo));
    });
  });

  // ---------- TanStack ----------
  private readonly colunasTanstack = computed<ColumnDef<typeof features, T>[]>(() =>
    this.colunas().map(c => ({
      id: c.id,
      header: c.cabecalho,
      accessorFn: (linha: T) => this.valorBruto(c, linha),
      enableSorting: c.ordenavel !== false,
      sortFn: (a, b, id) => compararValores(a.getValue(id), b.getValue(id))
    }))
  );

  private readonly paginacao = computed<PaginationState>(() => {
    const total = Math.max(1, Math.ceil(this.dadosFiltrados().length / this.tamanhoPagina()));
    const pedida = Math.max(1, Number(this.params().get('page')) || 1);
    return { pageIndex: Math.min(pedida, total) - 1, pageSize: this.tamanhoPagina() };
  });

  protected readonly table = injectTable<typeof features, T>(() => ({
    features,
    columns: this.colunasTanstack(),
    data: this.dadosFiltrados(),
    state: { sorting: this.ordenacaoUrl(), pagination: this.paginacao(), columnVisibility: this.visibilidade() },
    autoResetPageIndex: false,
    enableSortingRemoval: true,
    manualSorting: false,
    onSortingChange: () => undefined,
    onPaginationChange: () => undefined,
    onColumnVisibilityChange: () => undefined
  }));

  protected readonly colunasVisiveis = computed(() => {
    const visibilidade = this.visibilidade();
    return this.colunas().filter(c => visibilidade[c.id] !== false);
  });
  /**
   * Cartão do mobile: coluna com célula customizada e sem ordenação (ex.: foto) vira "mídia" ao lado do título;
   * o título é a primeira coluna comum; as demais viram pares rótulo/valor (até 4).
   */
  protected readonly colunasCartao = computed(() => {
    const visiveis = this.colunasVisiveis();
    const ehMidia = (c: ColunaTabela<T>) => c.ordenavel === false && !!this.celulaCustom(c);
    const midia = visiveis.find(ehMidia) ?? null;
    const restantes = visiveis.filter(c => c !== midia);
    return { midia, titulo: restantes[0] ?? null, resto: restantes.slice(1, 5) };
  });

  protected readonly linhasPagina = computed(() => this.table.getRowModel().rows.map(r => r.original));
  protected readonly totalFiltrado = computed(() => this.dadosFiltrados().length);
  protected readonly totalPaginas = computed(() => Math.max(1, Math.ceil(this.totalFiltrado() / this.tamanhoPagina())));
  protected readonly paginaAtual = computed(() => this.paginacao().pageIndex + 1);
  protected readonly intervalo = computed(() => {
    const total = this.totalFiltrado();
    if (!total) return '0 resultados';
    const ini = (this.paginaAtual() - 1) * this.tamanhoPagina() + 1;
    const fim = Math.min(total, ini + this.tamanhoPagina() - 1);
    const fmt = (n: number) => n.toLocaleString('pt-BR');
    const base = `${fmt(ini)}–${fmt(fim)} de ${fmt(total)} ${total === 1 ? 'resultado' : 'resultados'}`;
    return total !== this.dados().length ? `${base} (filtrado de ${fmt(this.dados().length)})` : base;
  });

  protected readonly semDados = computed(() => !this.carregando() && !this.erro() && this.dados().length === 0);
  protected readonly semResultados = computed(() => !this.carregando() && !this.erro() && this.dados().length > 0 && this.totalFiltrado() === 0);
  protected readonly esqueleto = [0, 1, 2, 3, 4, 5];

  // ---------- busca com debounce ----------
  protected readonly termo = signal('');
  private readonly digitado$ = new Subject<string>();

  constructor() {
    this.digitado$.pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed()).subscribe(valor => {
      this.atualizarUrl({ q: valor || null, page: null });
    });
    // mantém o campo de busca igual à URL (botão voltar, link compartilhado)
    effect(() => {
      const naUrl = this.busca();
      untracked(() => {
        if (this.termo() !== naUrl) this.termo.set(naUrl);
      });
    });
  }

  // ---------- valores e texto das células ----------
  protected valorBruto(coluna: ColunaTabela<T>, linha: T): unknown {
    return coluna.valor ? coluna.valor(linha) : (linha as Record<string, unknown>)[coluna.id];
  }

  private chaveFiltro(coluna: ColunaTabela<T>, linha: T): string {
    const valor = this.valorBruto(coluna, linha);
    return valor === null || valor === undefined ? '' : String(valor);
  }

  protected texto(coluna: ColunaTabela<T>, linha: T): string {
    if (coluna.formatar) return coluna.formatar(linha);
    const valor = this.valorBruto(coluna, linha);
    switch (coluna.tipo) {
      case 'moeda': return formatarMoeda(valor);
      case 'numero': return formatarNumero(valor);
      case 'data': return formatarData(valor);
      case 'booleano': return valor === null || valor === undefined ? '—' : valor ? 'Sim' : 'Não';
      case 'enum': return this.opcoesDe(coluna).find(o => o.valor === String(valor))?.rotulo ?? (valor === null || valor === undefined || valor === '' ? '—' : String(valor));
      default: return valor === null || valor === undefined || valor === '' ? '—' : String(valor);
    }
  }

  protected tomDe(coluna: ColunaTabela<T>, linha: T): TomStatus {
    if (coluna.tipo === 'booleano') return this.valorBruto(coluna, linha) ? 'success' : 'neutral';
    return this.opcoesDe(coluna).find(o => o.valor === this.chaveFiltro(coluna, linha))?.tom ?? 'neutral';
  }

  protected celulaCustom(coluna: ColunaTabela<T>): TemplateRef<ContextoCelula<T>> | null {
    return (this.celulas().find(c => c.id() === coluna.id)?.template as TemplateRef<ContextoCelula<T>> | undefined) ?? null;
  }

  protected contexto(linha: T): ContextoCelula<T> {
    return { $implicit: linha };
  }

  protected classeAlinhamento(coluna: ColunaTabela<T>): string {
    const fim = coluna.alinhar === 'fim' || coluna.tipo === 'moeda' || coluna.tipo === 'numero';
    if (coluna.alinhar === 'centro') return 'text-center';
    return fim ? 'text-right tabular-nums' : 'text-left';
  }

  protected classeResponsiva(coluna: ColunaTabela<T>): string {
    return coluna.prioridade === 'baixa' ? 'hidden 2xl:table-cell' : '';
  }

  protected classeLinha(): string {
    return ALTURA_LINHA[this.densidade()];
  }

  protected acoesDe(linha: T): AcaoLinha<T>[] {
    return this.acoesLinha().filter(a => !a.visivel || a.visivel(linha));
  }

  protected idDe(linha: T): string {
    return this.linhaId()(linha);
  }

  // ---------- ordenação ----------
  protected sentidoOrdenacao(id: string): 'ascending' | 'descending' | 'none' {
    const atual = this.ordenacaoUrl().find(s => s.id === id);
    return atual ? (atual.desc ? 'descending' : 'ascending') : 'none';
  }

  protected alternarOrdenacao(coluna: ColunaTabela<T>): void {
    if (coluna.ordenavel === false) return;
    const sentido = this.sentidoOrdenacao(coluna.id);
    if (sentido === 'none') this.atualizarUrl({ sort: coluna.id, dir: 'asc', page: null });
    else if (sentido === 'ascending') this.atualizarUrl({ sort: coluna.id, dir: 'desc', page: null });
    else this.atualizarUrl({ sort: null, dir: null, page: null });
  }

  // ---------- busca ----------
  protected aoDigitar(valor: string): void {
    this.termo.set(valor);
    this.digitado$.next(valor.trim());
  }

  protected limparBusca(): void {
    this.termo.set('');
    this.atualizarUrl({ q: null, page: null });
  }

  protected teclasBusca(evento: KeyboardEvent): void {
    if (evento.key === 'Escape' && this.termo()) {
      evento.stopPropagation();
      this.limparBusca();
    }
  }

  @HostListener('document:keydown', ['$event'])
  protected atalhoBusca(evento: KeyboardEvent): void {
    const alvo = evento.target as HTMLElement | null;
    const digitando = alvo && (alvo.tagName === 'INPUT' || alvo.tagName === 'TEXTAREA' || alvo.tagName === 'SELECT' || alvo.isContentEditable);
    if (evento.key === '/' && !digitando && !evento.ctrlKey && !evento.metaKey) {
      evento.preventDefault();
      this.campoBusca()?.nativeElement.focus();
    }
  }

  // ---------- filtros ----------
  protected filtroMarcado(colunaId: string, valor: string): boolean {
    return (this.filtros()[colunaId] ?? []).includes(valor);
  }

  protected alternarFiltro(colunaId: string, valor: string): void {
    const atuais = this.filtros()[colunaId] ?? [];
    const novos = atuais.includes(valor) ? atuais.filter(v => v !== valor) : [...atuais, valor];
    this.atualizarUrl({ [colunaId]: novos.length ? novos.join(',') : null, page: null });
  }

  protected removerFiltro(colunaId: string, valor: string): void {
    this.alternarFiltro(colunaId, valor);
  }

  protected limparFiltros(): void {
    const limpar: Record<string, null> = { q: null, page: null };
    for (const c of this.colunasFiltraveis()) limpar[c.id] = null;
    this.termo.set('');
    this.atualizarUrl(limpar);
  }

  // ---------- colunas, densidade e tamanho ----------
  protected alternarColuna(id: string): void {
    const visiveis = this.visibilidade();
    const ocultas = this.colunas().filter(c => (c.id === id ? visiveis[id] !== false : visiveis[c.id] === false)).map(c => c.id);
    this.salvarPreferencias({ ocultas });
  }

  protected restaurarColunas(): void {
    const resto: Preferencias = { ...this.prefs() };
    delete resto.ocultas;
    this.prefsLocais.set(resto);
    this.persistir();
  }

  protected definirDensidade(densidade: Densidade): void {
    this.salvarPreferencias({ densidade });
  }

  protected definirTamanho(valor: string | null | undefined): void {
    const tamanho = Number(valor);
    this.salvarPreferencias({ tamanho });
    this.atualizarUrl({ size: null, page: null });
  }

  protected irParaPagina(pagina: number): void {
    const alvo = Math.min(Math.max(1, pagina), this.totalPaginas());
    this.atualizarUrl({ page: alvo === 1 ? null : String(alvo) });
  }

  // ---------- exportação ----------
  protected exportar(): void {
    const colunas = this.colunasVisiveis();
    const linhas = this.table.getPrePaginatedRowModel().rows.map(r => r.original);
    baixarCsv(`${this.itens()}-${new Date().toISOString().slice(0, 10)}`, [
      colunas.map(c => c.cabecalho),
      ...linhas.map(l => colunas.map(c => this.texto(c, l)))
    ]);
  }

  // ---------- infraestrutura ----------
  private atualizarUrl(mudancas: Record<string, string | null>): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: mudancas,
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  private lerPreferencias(): Preferencias {
    try {
      const bruto = localStorage.getItem(`tabela:${this.tableId()}`);
      if (!bruto) return {};
      const salvas = JSON.parse(bruto) as Preferencias;
      if (salvas.versao !== VERSAO_PREFERENCIAS) return {};
      // colunas que deixaram de existir não ficam guardadas para sempre
      const existentes = new Set(this.colunas().map(c => c.id));
      return { ...salvas, ocultas: salvas.ocultas?.filter(id => existentes.has(id)) };
    } catch {
      return {};
    }
  }

  private salvarPreferencias(parcial: Preferencias): void {
    this.prefsLocais.set({ ...this.prefs(), ...parcial });
    this.persistir();
  }

  private persistir(): void {
    try {
      localStorage.setItem(`tabela:${this.tableId()}`, JSON.stringify({ ...this.prefs(), versao: VERSAO_PREFERENCIAS }));
    } catch {
      // armazenamento indisponível: preferências valem só nesta visita
    }
  }
}
