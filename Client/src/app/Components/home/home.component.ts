import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { AuthService } from '../../auth/auth.service';
import { EMPRESA_E_FUNCIONARIO, SOMENTE_EMPRESA, cargoPodeAcessar } from '../../core/permissoes';
import { TemaService } from '../../core/tema.service';
import { ChartWidgetComponent } from '../../shared/charts/chart-widget.component';
import { KpiCardComponent, TendenciaKpi } from '../../shared/charts/kpi-card.component';
import { lerTemaGrafico } from '../../shared/charts/tema-grafico';
import { formatarMoeda } from '../../shared/data-table/data-table.util';
import { ErrorStateComponent } from '../../shared/layout/error-state.component';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { DashboardService, ResumoDashboardDto } from './dashboard.service';

interface AtalhoDashboard {
  titulo: string;
  descricao: string;
  icone: string;
  rota: string;
  roles: readonly string[];
}

@Component({
  selector: 'app-home',
  imports: [RouterLink, NgIcon, HlmSkeleton, PageComponent, PageHeaderComponent, KpiCardComponent, ChartWidgetComponent, ErrorStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home.component.html'
})
export class HomeComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly dashboard = inject(DashboardService);
  private readonly tema = inject(TemaService);

  readonly nomeUsuario = signal('');
  readonly atalhos = signal<AtalhoDashboard[]>([]);

  // Resumo da operação (GET /dashboard/resumo): carrega e falha de forma independente dos atalhos
  readonly estado = signal<'carregando' | 'pronto' | 'erro'>('carregando');
  readonly resumo = signal<ResumoDashboardDto | null>(null);
  protected readonly esqueletos = [1, 2, 3, 4, 5];

  private readonly todosAtalhos: AtalhoDashboard[] = [
    { titulo: 'Veículos', descricao: 'Cadastro de veículos, grupos e planos de cobrança.', icone: 'lucideCar', rota: '/veiculos', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Clientes', descricao: 'Pessoas físicas e jurídicas cadastradas.', icone: 'lucideUsers', rota: '/clientes', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Condutores', descricao: 'Condutores habilitados para locação.', icone: 'lucideIdCard', rota: '/condutores', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Aluguéis', descricao: 'Locações em aberto, reservas e histórico.', icone: 'lucideReceipt', rota: '/alugueis', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Devoluções', descricao: 'Registro de devolução e recibo em PDF.', icone: 'lucideUndo2', rota: '/devolucoes', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Funcionários', descricao: 'Equipe com acesso ao sistema.', icone: 'lucideUserCog', rota: '/funcionarios', roles: SOMENTE_EMPRESA },
    { titulo: 'Parceiros', descricao: 'Postos, hotéis e seguradoras parceiras.', icone: 'lucideBriefcase', rota: '/parceiros', roles: SOMENTE_EMPRESA },
    { titulo: 'Cupons', descricao: 'Cupons de desconto para aluguéis.', icone: 'lucideTag', rota: '/cupons', roles: SOMENTE_EMPRESA },
    { titulo: 'Desafios de cupom', descricao: 'Metas que liberam cupons para clientes.', icone: 'lucideTrophy', rota: '/desafios-cupom', roles: SOMENTE_EMPRESA },
    { titulo: 'Taxas e serviços', descricao: 'Taxas adicionais cobradas no aluguel.', icone: 'lucideCoins', rota: '/taxas-servicos', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Monitoramento', descricao: 'Câmeras e leitura automática de placas.', icone: 'lucideVideo', rota: '/camera-monitor', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Verificação facial', descricao: 'Cadastro e verificação de identidade.', icone: 'lucideScanFace', rota: '/verificacao-facial', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Configurações', descricao: 'Preço do combustível e parâmetros gerais.', icone: 'lucideSettings', rota: '/configuracoes/combustivel', roles: SOMENTE_EMPRESA }
  ];

  /** Faturamento do mês contra o mês anterior. Sem base de comparação (mês anterior = 0) não inventa percentual. */
  readonly variacaoReceita = computed<{ texto: string; tendencia: TendenciaKpi }>(() => {
    const f = this.resumo()?.financeiro;
    if (!f || f.receitaMesAnterior <= 0) return { texto: 'sem mês anterior para comparar', tendencia: 'sem-base' };

    const pct = ((f.receitaDoMes - f.receitaMesAnterior) / f.receitaMesAnterior) * 100;
    if (Math.abs(pct) < 0.05) return { texto: 'igual ao mês anterior', tendencia: 'estavel' };

    const numero = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1, signDisplay: 'always' }).format(pct);
    return { texto: `${numero}% vs. mês anterior`, tendencia: pct > 0 ? 'alta' : 'queda' };
  });

  // Gráfico: barras de aluguéis (eixo a partir de zero) + linha de faturamento; cores dos tokens, e refeito ao trocar de tema.
  readonly opcoesGrafico = computed<Record<string, unknown>>(() => {
    this.tema.tema(); // dependência: recalcula as cores quando o tema muda
    const meses = this.resumo()?.ultimosMeses ?? [];
    const t = lerTemaGrafico();
    const eixo = { axisLine: { lineStyle: { color: t.grade } }, axisLabel: { color: t.textoSecundario, fontFamily: t.fonte } };
    const dinheiro = new Intl.NumberFormat('pt-BR', { notation: 'compact', maximumFractionDigits: 1 });

    return {
      aria: { enabled: true },
      color: [t.series[0], t.series[1]],
      textStyle: { fontFamily: t.fonte },
      // outerBounds* é o substituto do `containLabel` (obsoleto no ECharts 6): os rótulos dos eixos ficam dentro da grade
      grid: { left: 8, right: 8, top: 40, bottom: 8, outerBoundsMode: 'same', outerBoundsContain: 'axisLabel' },
      legend: { top: 0, textStyle: { color: t.texto, fontFamily: t.fonte } },
      tooltip: {
        trigger: 'axis',
        backgroundColor: t.fundoDica,
        borderColor: t.grade,
        textStyle: { color: t.textoDica, fontFamily: t.fonte },
        valueFormatter: (valor: number) => String(valor)
      },
      xAxis: { type: 'category', data: meses.map(m => this.rotuloMes(m.ano, m.mes)), ...eixo },
      yAxis: [
        { type: 'value', min: 0, minInterval: 1, name: 'Aluguéis', nameTextStyle: { color: t.textoSecundario }, splitLine: { lineStyle: { color: t.grade } }, ...eixo },
        {
          type: 'value', min: 0, name: 'Faturamento', nameTextStyle: { color: t.textoSecundario }, splitLine: { show: false },
          axisLabel: { color: t.textoSecundario, fontFamily: t.fonte, formatter: (v: number) => `R$ ${dinheiro.format(v)}` }
        }
      ],
      series: [
        { name: 'Aluguéis', type: 'bar', data: meses.map(m => m.alugueis), barMaxWidth: 36, itemStyle: { borderRadius: [6, 6, 0, 0] } },
        {
          name: 'Faturamento', type: 'line', yAxisIndex: 1, data: meses.map(m => m.receita), symbol: 'circle', symbolSize: 8,
          lineStyle: { width: 3 }, tooltip: { valueFormatter: (valor: number) => formatarMoeda(valor) }
        }
      ]
    };
  });

  ngOnInit(): void {
    const usuario = this.auth.getUsuario();
    this.nomeUsuario.set(usuario?.nomeCompleto ?? '');
    const cargo = usuario?.cargo ?? null;
    this.atalhos.set(this.todosAtalhos.filter(a => cargoPodeAcessar(cargo, a.roles)));
    this.carregarResumo();
  }

  carregarResumo(): void {
    this.estado.set('carregando');
    this.dashboard.resumo().subscribe({
      next: resumo => {
        this.resumo.set(resumo);
        this.estado.set('pronto');
      },
      error: () => this.estado.set('erro')
    });
  }

  // Intl direto: o pipe `currency` usaria o locale en-US ("R$1,080.00") porque o app não registra pt-BR.
  formatarMoeda(valor: number): string {
    return formatarMoeda(valor);
  }

  rotuloMes(ano: number, mes: number): string {
    const nome = new Intl.DateTimeFormat('pt-BR', { month: 'short', timeZone: 'UTC' }).format(new Date(Date.UTC(ano, mes - 1, 1)));
    return `${nome.replace('.', '')}/${String(ano).slice(2)}`;
  }
}
