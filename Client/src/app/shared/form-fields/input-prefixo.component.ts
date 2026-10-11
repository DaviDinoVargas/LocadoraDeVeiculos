import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Campo com um prefixo fixo à esquerda (ex.: "R$"). O prefixo é decorativo: o rótulo do campo já diz a unidade. */
@Component({
  selector: 'app-input-prefixo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'relative block [&_input]:pl-10' },
  template: `
    <span class="text-muted-foreground pointer-events-none absolute top-1/2 left-3 -translate-y-1/2" aria-hidden="true">{{ prefixo() }}</span>
    <ng-content />
  `
})
export class InputPrefixoComponent {
  readonly prefixo = input('R$');
}
