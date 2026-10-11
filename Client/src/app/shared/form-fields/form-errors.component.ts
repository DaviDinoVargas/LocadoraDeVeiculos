import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NgIcon } from '@ng-icons/core';
import { HlmAlertImports } from '@spartan-ng/helm/alert';

/** Alerta geral de erros do servidor (as mensagens reais da API), no topo do formulário. */
@Component({
  selector: 'app-form-errors',
  imports: [HlmAlertImports, NgIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // sem erros o elemento some, para não deixar um vão extra entre o cabeçalho e o formulário
  host: { '[class.hidden]': '!erros().length' },
  template: `
    @if (erros().length) {
      <div hlmAlert variant="destructive">
        <ng-icon name="lucideCircleAlert" aria-hidden="true" />
        <h2 hlmAlertTitle>{{ titulo() }}</h2>
        <div hlmAlertDescription>
          <ul class="list-disc pl-4">
            @for (erro of erros(); track $index) {
              <li>{{ erro }}</li>
            }
          </ul>
        </div>
      </div>
    }
  `
})
export class FormErrorsComponent {
  readonly erros = input<string[]>([]);
  readonly titulo = input('Não foi possível salvar');
}
