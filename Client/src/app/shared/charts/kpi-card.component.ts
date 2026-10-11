import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';

export type TendenciaKpi = 'alta' | 'queda' | 'estavel' | 'sem-base';

/**
 * Cartão de KPI (SKILL §8.3): valor grande com algarismos tabulares, apoio e variação com ícone ↑/↓ E
 * texto (a cor só reforça). Com `link`, o cartão inteiro leva à tela correspondente já filtrada (drill-down).
 */
@Component({
  selector: 'app-kpi-card',
  imports: [RouterLink, NgIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <a
      [routerLink]="link()"
      [queryParams]="parametros()"
      class="kpi border-border bg-card hover:bg-surface-container flex h-full flex-col gap-1 rounded-xl border p-5 transition-colors"
    >
      <span class="text-label-l text-muted-foreground">{{ rotulo() }}</span>
      <strong class="text-headline-m text-foreground tabular">
        {{ valor() }}@if (complemento()) {<small class="text-body-m text-muted-foreground font-normal"> {{ complemento() }}</small>}
      </strong>
      @if (alerta()) {
        <span class="kpi-alerta text-body-s text-warning flex items-center gap-1">
          <ng-icon name="lucideTriangleAlert" aria-hidden="true" />{{ alerta() }}
        </span>
      }
      @if (tendencia(); as t) {
        <span
          class="text-body-s flex items-center gap-1"
          [class.text-success]="t === 'alta'"
          [class.text-destructive]="t === 'queda'"
          [class.text-muted-foreground]="t === 'estavel' || t === 'sem-base'"
        >
          <ng-icon [name]="t === 'alta' ? 'lucideArrowUp' : t === 'queda' ? 'lucideArrowDown' : 'lucideCircleAlert'" aria-hidden="true" />
          {{ tendenciaTexto() }}
        </span>
      }
      @if (apoio()) {
        <span class="text-body-s text-muted-foreground">{{ apoio() }}</span>
      }
    </a>
  `
})
export class KpiCardComponent {
  readonly rotulo = input.required<string>();
  readonly valor = input.required<string | number>();
  /** Texto menor ao lado do valor ("de 10"). */
  readonly complemento = input('');
  readonly apoio = input('');
  readonly alerta = input('');
  readonly tendencia = input<TendenciaKpi | null>(null);
  readonly tendenciaTexto = input('');
  readonly link = input.required<string>();
  readonly consulta = input<Record<string, string> | null>(null);

  protected readonly parametros = computed(() => this.consulta() ?? undefined);
}
