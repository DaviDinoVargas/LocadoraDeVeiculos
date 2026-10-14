import { TestBed } from '@angular/core/testing';
import { provideIcons } from '@ng-icons/core';
import { lucideLayoutGrid, lucideRows3 } from '@ng-icons/lucide';
import { ChartWidgetComponent } from './chart-widget.component';

const OPCOES = {
  grid: { left: 8, right: 8, top: 40, bottom: 8, outerBoundsMode: 'same', outerBoundsContain: 'axisLabel' },
  xAxis: { type: 'category', data: ['jan', 'fev'] },
  yAxis: { type: 'value' },
  series: [{ type: 'bar', data: [1, 2] }]
};

describe('ChartWidgetComponent — gráfico com alternativa em tabela', () => {
  let avisos: string[];

  const montar = (entradas: Record<string, unknown> = {}) => {
    TestBed.configureTestingModule({ imports: [ChartWidgetComponent], providers: [provideIcons({ lucideLayoutGrid, lucideRows3 })] });
    const fixture = TestBed.createComponent(ChartWidgetComponent);
    fixture.componentRef.setInput('titulo', 'Aluguéis por mês');
    fixture.componentRef.setInput('opcoes', OPCOES);
    for (const [nome, valor] of Object.entries(entradas)) fixture.componentRef.setInput(nome, valor);
    fixture.detectChanges();
    return fixture;
  };

  beforeEach(() => {
    avisos = [];
    spyOn(console, 'warn').and.callFake((...args: unknown[]) => void avisos.push(args.join(' ')));
    spyOn(console, 'error').and.callFake((...args: unknown[]) => void avisos.push(args.join(' ')));
  });

  it('mostra título, o gráfico acessível e o botão "Ver como tabela"', () => {
    const raiz = montar({ descricaoAcessivel: 'Barras de aluguéis' }).nativeElement as HTMLElement;

    expect(raiz.textContent).toContain('Aluguéis por mês');
    expect(raiz.querySelector('[echarts]')?.getAttribute('aria-label')).toBe('Barras de aluguéis');
    expect(raiz.textContent).toContain('Ver como tabela');
  });

  it('alternar para tabela e voltar não gera aviso do ECharts (obsoleto ou largura 0) nem erro', () => {
    const fixture = montar();
    const raiz = fixture.nativeElement as HTMLElement;
    const alternar = () => (raiz.querySelector('header button') as HTMLButtonElement).click();

    alternar();
    fixture.detectChanges();
    expect(raiz.querySelector('[echarts]')).toBeNull();
    expect(raiz.textContent).toContain('Ver gráfico');

    alternar();
    fixture.detectChanges();

    expect(raiz.querySelector('[echarts]')).not.toBeNull();
    expect(avisos.filter(a => /containLabel|outerBounds|DOM width|clientWidth/i.test(a))).toEqual([]);
  });

  it('carregando mostra o esqueleto e esconde o gráfico e o botão', () => {
    const raiz = montar({ carregando: true }).nativeElement as HTMLElement;

    expect(raiz.querySelector('[echarts]')).toBeNull();
    expect(raiz.querySelector('hlm-skeleton')).not.toBeNull();
    expect(raiz.textContent).not.toContain('Ver como tabela');
  });

  it('erro mostra o estado de erro e pede para tentar de novo', () => {
    const fixture = montar({ erro: true });
    let pediu = 0;
    fixture.componentInstance.recarregar.subscribe(() => pediu++);

    const botao = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).find(b => /tentar/i.test(b.textContent ?? ''));
    botao?.click();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Não foi possível carregar o gráfico');
    expect(pediu).toBe(1);
  });
});
