import { ChangeDetectionStrategy, Component, Injectable, inject } from '@angular/core';
import { NgIcon } from '@ng-icons/core';
import { BrnDialogRef, injectBrnDialogContext } from '@spartan-ng/brain/dialog';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmDialogDescription, HlmDialogFooter, HlmDialogHeader, HlmDialogService, HlmDialogTitle } from '@spartan-ng/helm/dialog';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

export interface OpcoesConfirmacao {
  titulo?: string;
  mensagem: string;
  /** Rótulo do botão de confirmação: verbo específico ("Excluir", "Iniciar aluguel"), nunca "OK". */
  confirmar?: string;
  cancelar?: string;
  /** Ação irreversível: botão destrutivo e aviso de que não dá para desfazer. */
  destrutivo?: boolean;
}

type DadosConfirmacao = Required<OpcoesConfirmacao>;

@Component({
  selector: 'app-confirm-dialog',
  imports: [HlmButtonImports, HlmDialogHeader, HlmDialogTitle, HlmDialogDescription, HlmDialogFooter, NgIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hlm-dialog-header class="gap-2">
      <h2 hlmDialogTitle class="text-title-l flex items-center gap-2 leading-7">
        @if (dados.destrutivo) {
          <ng-icon name="lucideTriangleAlert" class="text-destructive text-xl" aria-hidden="true" />
        }
        {{ dados.titulo }}
      </h2>
      <p hlmDialogDescription class="text-body-m text-muted-foreground">{{ dados.mensagem }}</p>
      @if (dados.destrutivo) {
        <p class="text-body-s text-muted-foreground">Esta ação não pode ser desfeita.</p>
      }
    </hlm-dialog-header>
    <hlm-dialog-footer>
      <!-- foco inicial no botão seguro (Cancelar), como pede o padrão de ações destrutivas -->
      <button hlmBtn variant="outline" type="button" (click)="fechar(false)">{{ dados.cancelar }}</button>
      <button
        hlmBtn
        type="button"
        [variant]="dados.destrutivo ? 'destructive' : 'default'"
        (click)="fechar(true)"
      >
        {{ dados.confirmar }}
      </button>
    </hlm-dialog-footer>
  `
})
export class ConfirmDialogComponent {
  private readonly ref = inject<BrnDialogRef<boolean>>(BrnDialogRef);
  protected readonly dados = injectBrnDialogContext<DadosConfirmacao>();

  protected fechar(resultado: boolean): void {
    this.ref.close(resultado);
  }
}

/**
 * Substitui o confirm() nativo do navegador (bloqueante, sem estilo e sem acessibilidade) por um
 * diálogo do Spartan com foco inicial em "Cancelar". Esc, clique fora ou "Cancelar" resultam em false.
 *
 *   this.confirmacao.confirmar({ mensagem: 'Excluir este veículo?', confirmar: 'Excluir', destrutivo: true })
 *     .pipe(filter(Boolean)).subscribe(() => ...)
 */
@Injectable({ providedIn: 'root' })
export class ConfirmacaoService {
  private readonly dialogos = inject(HlmDialogService);

  confirmar(opcoes: OpcoesConfirmacao | string): Observable<boolean> {
    const base = typeof opcoes === 'string' ? { mensagem: opcoes } : opcoes;
    const dados: DadosConfirmacao = {
      titulo: 'Confirmar ação',
      confirmar: 'Confirmar',
      cancelar: 'Cancelar',
      destrutivo: false,
      ...base
    };

    const ref = this.dialogos.open<boolean, DadosConfirmacao>(ConfirmDialogComponent, {
      context: dados,
      showCloseButton: false,
      role: 'alertdialog',
      autoFocus: 'first-tabbable',
      closeOnOutsidePointerEvents: true
    });
    return ref.closed$.pipe(map(resultado => resultado === true));
  }
}
