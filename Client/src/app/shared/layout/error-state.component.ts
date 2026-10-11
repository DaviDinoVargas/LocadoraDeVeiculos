import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';

/** Erro de carregamento: diz o que aconteceu e oferece "Tentar novamente"; nunca tela em branco. */
@Component({
  selector: 'app-error-state',
  imports: [HlmButtonImports, NgIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section
      role="alert"
      class="border-border bg-card flex flex-col items-center gap-3 rounded-xl border px-6 py-10 text-center"
    >
      <span class="bg-destructive/10 text-destructive flex size-12 items-center justify-center rounded-full">
        <ng-icon name="lucideTriangleAlert" class="text-2xl" aria-hidden="true" />
      </span>
      <h2 class="text-title-m text-foreground">{{ titulo() }}</h2>
      <p class="text-body-m text-muted-foreground max-w-prose">{{ descricao() }}</p>
      @if (tentarNovamente()) {
        <button hlmBtn variant="outline" type="button" (click)="repetir.emit()">
          <ng-icon name="lucideRefreshCw" aria-hidden="true" />
          Tentar novamente
        </button>
      }
    </section>
  `
})
export class ErrorStateComponent {
  readonly titulo = input('Não foi possível carregar');
  readonly descricao = input('Algo deu errado ao buscar os dados. Tente novamente em instantes.');
  readonly tentarNovamente = input(true);
  readonly repetir = output<void>();
}
