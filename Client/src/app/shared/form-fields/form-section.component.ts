import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Seção de formulário: título (Title M), descrição curta e uma grade de 12 colunas para os campos
 * (cada campo escolhe a largura com `col-span-12 md:col-span-6`...). Em mobile tudo vira uma coluna.
 */
@Component({
  selector: 'app-form-section',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
  template: `
    <section class="border-border bg-card flex flex-col gap-4 rounded-xl border p-5 sm:p-6" [attr.aria-labelledby]="titulo() ? 'secao-' + idSecao : null">
      @if (titulo()) {
        <header>
          <h2 class="text-title-m text-foreground" [id]="'secao-' + idSecao">{{ titulo() }}</h2>
          @if (descricao()) {
            <p class="text-body-m text-muted-foreground mt-1">{{ descricao() }}</p>
          }
        </header>
      }
      <div class="grid grid-cols-12 gap-x-4 gap-y-5"><ng-content /></div>
    </section>
  `
})
export class FormSectionComponent {
  private static proximo = 0;
  readonly titulo = input('');
  readonly descricao = input('');
  protected readonly idSecao = FormSectionComponent.proximo++;
}
