import { ChangeDetectionStrategy, Component, HostListener, input, output } from '@angular/core';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';

/**
 * Barra de ações do formulário (SKILL §17.3), fixa na base da área de conteúdo: "Cancelar" (outline)
 * à esquerda do botão principal "Salvar" (filled, tipo submit), mais o aviso "Alterações não salvas".
 * Deve ficar DENTRO do <form> para o botão de envio funcionar. Também avisa ao fechar a aba com
 * alterações pendentes.
 */
@Component({
  selector: 'app-form-actions',
  imports: [HlmButtonImports, NgIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'sticky bottom-0 z-10 -mx-4 -mb-6 mt-2 block sm:-mx-6 lg:-mx-8' },
  template: `
    <div class="border-border bg-surface-container flex flex-wrap items-center justify-between gap-3 border-t px-4 py-3 sm:px-6 lg:px-8">
      <p class="text-body-s text-muted-foreground flex items-center gap-1.5" role="status">
        @if (alterado()) {
          <ng-icon name="lucideCircleAlert" class="text-base" aria-hidden="true" />
          Alterações não salvas
        }
      </p>
      <div class="flex items-center gap-2">
        <button hlmBtn variant="outline" type="button" [disabled]="salvando()" (click)="cancelar.emit()">Cancelar</button>
        <button hlmBtn type="submit" [disabled]="salvando()" [attr.aria-busy]="salvando()">
          @if (salvando()) {
            <ng-icon name="lucideLoader2" class="animate-spin motion-reduce:animate-none" aria-hidden="true" />
            Salvando…
          } @else {
            {{ rotuloSalvar() }}
          }
        </button>
      </div>
    </div>
  `
})
export class FormActionsComponent {
  readonly salvando = input(false);
  readonly alterado = input(false);
  readonly rotuloSalvar = input('Salvar');
  readonly cancelar = output<void>();

  @HostListener('window:beforeunload', ['$event'])
  protected avisarAoFechar(evento: BeforeUnloadEvent): void {
    if (this.alterado()) {
      evento.preventDefault();
    }
  }
}
