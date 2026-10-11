import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NgIcon } from '@ng-icons/core';

/** Estado vazio: explica o porquê, indica o próximo passo; a ação (se houver) é projetada. */
@Component({
  selector: 'app-empty-state',
  imports: [NgIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="border-border bg-card flex flex-col items-center gap-3 rounded-xl border border-dashed px-6 py-12 text-center">
      <span class="bg-secondary text-secondary-foreground flex size-12 items-center justify-center rounded-full">
        <ng-icon [name]="icone()" class="text-2xl" aria-hidden="true" />
      </span>
      <h2 class="text-title-m text-foreground">{{ titulo() }}</h2>
      @if (descricao()) {
        <p class="text-body-m text-muted-foreground max-w-prose">{{ descricao() }}</p>
      }
      <div class="mt-1 flex flex-wrap justify-center gap-2"><ng-content /></div>
    </section>
  `
})
export class EmptyStateComponent {
  readonly titulo = input.required<string>();
  readonly descricao = input('');
  readonly icone = input('lucideInbox');
}
