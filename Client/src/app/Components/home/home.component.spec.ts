import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { HomeComponent } from './home.component';
import { DashboardService, ResumoDashboardDto } from './dashboard.service';
import { AuthService } from '../../auth/auth.service';

describe('HomeComponent — resumo da operação', () => {
  let fixture: ComponentFixture<HomeComponent>;
  let component: HomeComponent;
  let dashboard: jasmine.SpyObj<DashboardService>;

  const resumo: ResumoDashboardDto = {
    frota: { total: 10, emUso: 4, disponiveis: 5 },
    alugueis: { emAndamento: 4, reservados: 2, atrasados: 1 },
    financeiro: { receitaDoMes: 1080, receitaMesAnterior: 1000, devolucoesDoMes: 3 },
    totalClientes: 25,
    ultimosMeses: [
      { ano: 2026, mes: 5, alugueis: 0, receita: 0 },
      { ano: 2026, mes: 6, alugueis: 2, receita: 400 },
      { ano: 2026, mes: 7, alugueis: 8, receita: 1500 },
      { ano: 2026, mes: 8, alugueis: 4, receita: 700 },
      { ano: 2026, mes: 9, alugueis: 6, receita: 1000 },
      { ano: 2026, mes: 10, alugueis: 7, receita: 1080 }
    ],
    geradoEm: '2026-10-15T12:00:00Z'
  };

  const montar = (cargo = 'Empresa') => {
    TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [
        provideRouter([]),
        { provide: DashboardService, useValue: dashboard },
        { provide: AuthService, useValue: { getUsuario: () => ({ cargo, nomeCompleto: 'Fulano' }) } }
      ]
    });
    fixture = TestBed.createComponent(HomeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  };

  const texto = () => (fixture.nativeElement as HTMLElement).textContent?.replace(/\s+/g, ' ') ?? '';

  beforeEach(() => {
    dashboard = jasmine.createSpyObj('DashboardService', ['resumo']);
    dashboard.resumo.and.returnValue(of(resumo));
  });

  it('mostra os KPIs vindos do servidor', () => {
    montar();

    expect(component.estado()).toBe('pronto');
    expect(texto()).toContain('Frota disponível');
    expect(texto()).toContain('5 de 10');
    expect(texto()).toContain('4 em uso');
    expect(texto()).toContain('Aluguéis em andamento');
    expect(texto()).toContain('Reservados');
    expect(texto()).toContain('Faturamento do mês');
    expect(texto()).toContain('R$ 1.080,00');
    expect(texto()).toContain('25');
  });

  it('atrasados aparecem com ícone E texto (não só cor); sem atrasados diz "nenhum atrasado"', () => {
    montar();
    expect(texto()).toContain('1 atrasado(s)');
    expect(fixture.nativeElement.querySelector('.kpi-alerta ng-icon')).not.toBeNull();

    TestBed.resetTestingModule();
    dashboard.resumo.and.returnValue(of({ ...resumo, alugueis: { ...resumo.alugueis, atrasados: 0 } }));
    montar();
    expect(texto()).toContain('nenhum atrasado');
  });

  it('cada KPI leva à lista correspondente já filtrada (drill-down)', () => {
    montar();

    const links = Array.from(fixture.nativeElement.querySelectorAll('a.kpi') as NodeListOf<HTMLAnchorElement>).map(a => a.getAttribute('href'));
    expect(links).toContain('/alugueis?status=EmAndamento');
    expect(links).toContain('/alugueis?status=Reservado');
    expect(links).toContain('/veiculos');
    expect(links).toContain('/devolucoes');
    expect(links).toContain('/clientes');
  });

  it('variação do faturamento: percentual com sinal; sem mês anterior não inventa número', () => {
    montar();
    expect(component.variacaoReceita()).toEqual({ texto: '+8% vs. mês anterior', tendencia: 'alta' });

    component.resumo.set({ ...resumo, financeiro: { ...resumo.financeiro, receitaDoMes: 500 } });
    expect(component.variacaoReceita()).toEqual({ texto: '-50% vs. mês anterior', tendencia: 'queda' });

    component.resumo.set({ ...resumo, financeiro: { ...resumo.financeiro, receitaMesAnterior: 0 } });
    expect(component.variacaoReceita().tendencia).toBe('sem-base');

    component.resumo.set({ ...resumo, financeiro: { ...resumo.financeiro, receitaDoMes: 1000 } });
    expect(component.variacaoReceita().tendencia).toBe('estavel');
  });

  it('a variação do faturamento mostra ícone ↑/↓ e texto', () => {
    montar();

    const kpi = Array.from(fixture.nativeElement.querySelectorAll('a.kpi') as NodeListOf<HTMLElement>).find(a => a.textContent?.includes('Faturamento'))!;
    expect(kpi.textContent).toContain('+8% vs. mês anterior');
    expect(kpi.querySelector('ng-icon')).not.toBeNull();
  });

  it('o gráfico lê as cores dos tokens, tem eixo de aluguéis a partir de zero e série em barra + linha (não só cor)', () => {
    montar();

    const opcoes = component.opcoesGrafico() as { series: { type: string }[]; yAxis: { min: number }[]; color: string[] };
    expect(opcoes.series.map(s => s.type)).toEqual(['bar', 'line']);
    expect(opcoes.yAxis[0].min).toBe(0);
    expect(opcoes.color.length).toBe(2);
    expect(opcoes.color.every(c => !!c)).toBeTrue();
  });

  it('rótulo do mês em pt-BR e alternativa em tabela para o gráfico', () => {
    montar();

    expect(component.rotuloMes(2026, 10)).toMatch(/^out\/26$/);
    expect(texto()).toContain('Ver como tabela');

    (Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[]).find(b => b.textContent?.includes('Ver como tabela'))!.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('table.grafico-tabela')).not.toBeNull();
    expect(texto()).toContain('R$ 1.500,00');
  });

  it('falha só do resumo: mostra erro com "Tentar novamente" e os atalhos continuam funcionando', () => {
    dashboard.resumo.and.returnValue(throwError(() => new Error('falhou')));
    montar();

    expect(component.estado()).toBe('erro');
    expect(texto()).toContain('Não foi possível carregar o resumo');
    expect(fixture.nativeElement.querySelectorAll('a.atalho-card').length).toBeGreaterThan(0);

    dashboard.resumo.and.returnValue(of(resumo));
    (Array.from(fixture.nativeElement.querySelectorAll('[role="alert"] button')) as HTMLButtonElement[])[0].click();
    fixture.detectChanges();

    expect(component.estado()).toBe('pronto');
  });

  it('enquanto carrega mostra esqueletos (aria-busy), sem layout vazio', () => {
    dashboard.resumo.and.returnValue(new Observable<ResumoDashboardDto>(() => {}));
    montar();

    expect(component.estado()).toBe('carregando');
    expect(fixture.nativeElement.querySelector('[aria-busy="true"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelectorAll('.kpi-esqueleto').length).toBe(5);
  });

  it('o Funcionario também vê o resumo, mas só os atalhos que a API deixa', () => {
    montar('Funcionario');

    expect(component.estado()).toBe('pronto');
    const rotas = component.atalhos().map(a => a.rota);
    expect(rotas).toContain('/clientes');
    expect(rotas).not.toContain('/funcionarios');
  });
});
