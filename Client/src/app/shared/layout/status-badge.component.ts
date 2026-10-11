import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { NgIcon } from '@ng-icons/core';

export type TomStatus = 'success' | 'warning' | 'error' | 'neutral' | 'info';

const ESTILO: Record<TomStatus, { classes: string; icone: string }> = {
  success: { classes: 'bg-success-container text-on-success-container', icone: 'lucideCircleCheck' },
  warning: { classes: 'bg-warning-container text-on-warning-container', icone: 'lucideTriangleAlert' },
  error: { classes: 'bg-error-container text-on-error-container', icone: 'lucideCircleX' },
  info: { classes: 'bg-primary-container text-on-primary-container', icone: 'lucideInfo' },
  neutral: { classes: 'bg-muted text-muted-foreground', icone: 'lucideClock' }
};

/** Status SEMPRE com ícone + texto (nunca só cor). */
@Component({
  selector: 'app-status-badge',
  imports: [NgIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="text-label-m inline-flex items-center gap-1 rounded-full px-2.5 py-1 whitespace-nowrap" [class]="estilo().classes">
      <ng-icon [name]="icone() || estilo().icone" class="text-sm" aria-hidden="true" />
      {{ texto() }}
    </span>
  `
})
export class StatusBadgeComponent {
  readonly texto = input.required<string>();
  readonly tom = input<TomStatus>('neutral');
  readonly icone = input('');

  protected readonly estilo = computed(() => ESTILO[this.tom()]);
}
