import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { BarChart, LineChart } from 'echarts/charts';
import { AriaComponent, GridComponent, LegendComponent, TooltipComponent } from 'echarts/components';
import * as echarts from 'echarts/core';
import { CanvasRenderer } from 'echarts/renderers';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import { ErrorStateComponent } from '../layout/error-state.component';

// Só o que o app usa (barras, linhas, grade, dica, legenda, acessibilidade): o ECharts completo pesa muito.
echarts.use([BarChart, LineChart, GridComponent, TooltipComponent, LegendComponent, AriaComponent, CanvasRenderer]);

/**
 * Widget de gráfico (SKILL §19): título, descrição, ação "Ver como tabela" (alternativa acessível a
 * todo gráfico), estados próprios (carregando/erro com "Tentar novamente") e o gráfico em si.
 * O gráfico nunca derruba a página: cada widget falha de forma independente.
 *
 *   <app-chart-widget titulo="..." [opcoes]="opcoes" [carregando]="..." [erro]="..." (recarregar)="...">
 *     <table tabela>...</table>   <!-- alternativa em tabela -->
 *   </app-chart-widget>
 */
@Component({
  selector: 'app-chart-widget',
  imports: [NgxEchartsDirective, NgIcon, HlmButtonImports, HlmSkeleton, ErrorStateComponent],
  providers: [provideEchartsCore({ echarts })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="border-border bg-card flex flex-col gap-4 rounded-xl border p-5" [attr.aria-busy]="carregando()">
      <header class="flex items-start justify-between gap-3">
        <div>
          <h2 class="text-title-m text-foreground">{{ titulo() }}</h2>
          @if (descricao()) {
            <p class="text-body-s text-muted-foreground mt-0.5">{{ descricao() }}</p>
          }
        </div>
        @if (!carregando() && !erro()) {
          <button hlmBtn variant="ghost" size="sm" type="button" [attr.aria-pressed]="comoTabela()" (click)="comoTabela.set(!comoTabela())">
            <ng-icon [name]="comoTabela() ? 'lucideLayoutGrid' : 'lucideRows3'" aria-hidden="true" />
            {{ comoTabela() ? 'Ver gráfico' : 'Ver como tabela' }}
          </button>
        }
      </header>

      @if (carregando()) {
        <hlm-skeleton class="block w-full rounded-lg" [style.height.px]="altura()" />
      } @else if (erro()) {
        <app-error-state titulo="Não foi possível carregar o gráfico" (repetir)="recarregar.emit()" />
      } @else if (comoTabela()) {
        <div class="overflow-x-auto"><ng-content select="[tabela]" /></div>
      } @else {
        <div echarts class="w-full" [options]="opcoes()" [style.height.px]="altura()" role="img" [attr.aria-label]="descricaoAcessivel() || titulo()"></div>
      }
    </section>
  `
})
export class ChartWidgetComponent {
  readonly titulo = input.required<string>();
  readonly descricao = input('');
  readonly descricaoAcessivel = input('');
  // ECharts aceita um objeto de opções amplo; o tipo exato vem da própria biblioteca
  readonly opcoes = input.required<Record<string, unknown>>();
  readonly carregando = input(false);
  readonly erro = input(false);
  readonly altura = input(280);
  readonly recarregar = output<void>();

  protected readonly comoTabela = signal(false);
}
