import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Contêiner de página: largura máxima e espaçamento padrão entre blocos (header, filtros, tabela...).
 * `largura`: `total` (listas e dashboards, até 1440 px), `form` (formulários, até 64rem) ou `estreita`
 * (formulários curtos, até 40rem). A margem lateral vem do <main> da casca.
 */
@Component({
  selector: 'app-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'mx-auto flex w-full flex-col gap-6',
    '[class.max-w-[1440px]]': "largura() === 'total'",
    '[class.max-w-5xl]': "largura() === 'form'",
    '[class.max-w-2xl]': "largura() === 'estreita'"
  },
  template: `<ng-content />`
})
export class PageComponent {
  readonly largura = input<'total' | 'form' | 'estreita'>('total');
}
