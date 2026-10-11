import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NgIcon } from '@ng-icons/core';
import { RouterLink } from '@angular/router';

/**
 * Cabeçalho de página: título (Headline S), apoio e a ÚNICA ação primária à direita
 * (conteúdo projetado com `acoes`). Em mobile as ações vão para baixo do título.
 */
@Component({
  selector: 'app-page-header',
  imports: [RouterLink, NgIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
      <div class="min-w-0">
        @if (voltarPara()) {
          <a
            [routerLink]="voltarPara()"
            class="text-label-l text-muted-foreground hover:text-foreground mb-1 inline-flex min-h-8 items-center gap-1"
          >
            <ng-icon name="lucideArrowLeft" class="text-base" aria-hidden="true" />
            {{ rotuloVoltar() }}
          </a>
        }
        <h1 class="text-headline-s text-foreground">{{ titulo() }}</h1>
        @if (descricao()) {
          <p class="text-body-m text-muted-foreground mt-1 max-w-prose">{{ descricao() }}</p>
        }
      </div>
      <div class="flex flex-wrap items-center gap-2 sm:justify-end"><ng-content select="[acoes]" /></div>
    </header>
  `
})
export class PageHeaderComponent {
  readonly titulo = input.required<string>();
  readonly descricao = input('');
  readonly voltarPara = input<string | null>(null);
  readonly rotuloVoltar = input('Voltar');
}
