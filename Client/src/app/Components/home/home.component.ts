// home.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../auth/auth.service';
import { DashboardService, ResumoDashboardDto } from './dashboard.service';
import { EMPRESA_E_FUNCIONARIO, SOMENTE_EMPRESA, cargoPodeAcessar } from '../../core/permissoes';

interface AtalhoDashboard {
  titulo: string;
  descricao: string;
  icone: string;
  rota: string;
  roles: readonly string[];
}

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss']
})
export class HomeComponent implements OnInit {
  nomeUsuario = '';
  atalhos: AtalhoDashboard[] = [];

  // Resumo da operação (GET /dashboard/resumo): carrega e falha de forma independente dos atalhos
  estado: 'carregando' | 'pronto' | 'erro' = 'carregando';
  resumo: ResumoDashboardDto | null = null;

  private readonly todosAtalhos: AtalhoDashboard[] = [
    { titulo: 'Veículos', descricao: 'Cadastro de veículos, grupos e planos de cobrança.', icone: 'bi-car-front', rota: '/veiculos', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Clientes', descricao: 'Pessoas físicas e jurídicas cadastradas.', icone: 'bi-people', rota: '/clientes', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Condutores', descricao: 'Condutores habilitados para locação.', icone: 'bi-person-badge', rota: '/condutores', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Aluguéis', descricao: 'Locações em aberto, reservas e histórico.', icone: 'bi-receipt', rota: '/alugueis', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Devoluções', descricao: 'Registro de devolução e recibo em PDF.', icone: 'bi-arrow-return-left', rota: '/devolucoes', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Funcionários', descricao: 'Equipe com acesso ao sistema.', icone: 'bi-person-workspace', rota: '/funcionarios', roles: SOMENTE_EMPRESA },
    { titulo: 'Parceiros', descricao: 'Postos, hotéis e seguradoras parceiras.', icone: 'bi-building', rota: '/parceiros', roles: SOMENTE_EMPRESA },
    { titulo: 'Cupons', descricao: 'Cupons de desconto para aluguéis.', icone: 'bi-tag', rota: '/cupons', roles: SOMENTE_EMPRESA },
    { titulo: 'Desafios de Cupom', descricao: 'Metas que liberam cupons para clientes.', icone: 'bi-trophy', rota: '/desafios-cupom', roles: SOMENTE_EMPRESA },
    { titulo: 'Taxas e Serviços', descricao: 'Taxas adicionais cobradas no aluguel.', icone: 'bi-cash-coin', rota: '/taxas-servicos', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Monitoramento', descricao: 'Câmeras e leitura automática de placas.', icone: 'bi-camera-video', rota: '/camera-monitor', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Verificação Facial', descricao: 'Cadastro e verificação de identidade.', icone: 'bi-person-bounding-box', rota: '/verificacao-facial', roles: EMPRESA_E_FUNCIONARIO },
    { titulo: 'Configurações', descricao: 'Preço do combustível e parâmetros gerais.', icone: 'bi-gear', rota: '/configuracoes/combustivel', roles: SOMENTE_EMPRESA }
  ];

  constructor(private auth: AuthService, private dashboard: DashboardService) {}

  ngOnInit(): void {
    const usuario = this.auth.getUsuario();
    this.nomeUsuario = usuario?.nomeCompleto ?? '';
    const cargo = usuario?.cargo ?? null;
    this.atalhos = this.todosAtalhos.filter(a => cargoPodeAcessar(cargo, a.roles));
    this.carregarResumo();
  }

  carregarResumo(): void {
    this.estado = 'carregando';
    this.dashboard.resumo().subscribe({
      next: resumo => {
        this.resumo = resumo;
        this.estado = 'pronto';
      },
      error: () => (this.estado = 'erro')
    });
  }

  /** Faturamento do mês contra o mês anterior. Sem base de comparação (mês anterior = 0) não inventa percentual. */
  variacaoReceita(): { texto: string; tendencia: 'alta' | 'queda' | 'estavel' | 'sem-base' } {
    const f = this.resumo?.financeiro;
    if (!f || f.receitaMesAnterior <= 0) return { texto: 'sem mês anterior para comparar', tendencia: 'sem-base' };

    const pct = ((f.receitaDoMes - f.receitaMesAnterior) / f.receitaMesAnterior) * 100;
    if (Math.abs(pct) < 0.05) return { texto: 'igual ao mês anterior', tendencia: 'estavel' };

    const numero = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1, signDisplay: 'always' }).format(pct);
    return { texto: `${numero}% vs. mês anterior`, tendencia: pct > 0 ? 'alta' : 'queda' };
  }

  /** Altura (%) da barra de aluguéis do mês; o maior mês ocupa 100% e mês com valor nunca some (mínimo 4%). */
  alturaBarra(alugueis: number): number {
    const maximo = Math.max(1, ...(this.resumo?.ultimosMeses.map(m => m.alugueis) ?? [1]));
    return alugueis <= 0 ? 0 : Math.max(4, Math.round((alugueis / maximo) * 100));
  }

  // Intl direto: o pipe `currency` usaria o locale en-US ("R$1,080.00") porque o app não registra pt-BR.
  formatarMoeda(valor: number): string {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(valor ?? 0);
  }

  rotuloMes(ano: number, mes: number): string {
    const nome = new Intl.DateTimeFormat('pt-BR', { month: 'short', timeZone: 'UTC' }).format(new Date(Date.UTC(ano, mes - 1, 1)));
    return `${nome.replace('.', '')}/${String(ano).slice(2)}`;
  }
}
