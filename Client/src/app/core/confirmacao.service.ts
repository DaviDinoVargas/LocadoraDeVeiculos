import { Component, Injectable, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

export interface OpcoesConfirmacao {
  titulo?: string;
  mensagem: string;
  /** Rótulo do botão de confirmação: verbo específico ("Excluir", "Iniciar aluguel"), nunca "OK". */
  confirmar?: string;
  cancelar?: string;
  /** Ação irreversível: botão vermelho e aviso de que não dá para desfazer. */
  destrutivo?: boolean;
}

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [MatDialogModule],
  template: `
    <h2 mat-dialog-title id="confirm-titulo">{{ dados.titulo }}</h2>
    <mat-dialog-content>
      <p class="mb-1">{{ dados.mensagem }}</p>
      @if (dados.destrutivo) {
        <p class="small text-muted mb-0">Esta ação não pode ser desfeita.</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <!-- foco inicial no botão seguro (Cancelar), como pede o padrão de ações destrutivas -->
      <button type="button" class="btn btn-secondary" [mat-dialog-close]="false" cdkFocusInitial>{{ dados.cancelar }}</button>
      <button type="button" class="btn" [class.btn-danger]="dados.destrutivo" [class.btn-primary]="!dados.destrutivo" [mat-dialog-close]="true">{{ dados.confirmar }}</button>
    </mat-dialog-actions>
  `
})
export class ConfirmDialogComponent {
  protected readonly dados = inject<Required<OpcoesConfirmacao>>(MAT_DIALOG_DATA);
}

/**
 * Substitui o confirm() nativo do navegador (bloqueante, sem estilo e sem acessibilidade) por um
 * diálogo do Angular Material com foco inicial em "Cancelar". Esc, clique fora ou "Cancelar"
 * resultam em false.
 *
 *   this.confirmacao.confirmar({ mensagem: 'Excluir este veículo?', confirmar: 'Excluir', destrutivo: true })
 *     .pipe(filter(Boolean)).subscribe(() => ...)
 */
@Injectable({ providedIn: 'root' })
export class ConfirmacaoService {
  private readonly dialog = inject(MatDialog);

  confirmar(opcoes: OpcoesConfirmacao | string): Observable<boolean> {
    const base = typeof opcoes === 'string' ? { mensagem: opcoes } : opcoes;
    const dados: Required<OpcoesConfirmacao> = {
      titulo: 'Confirmar ação',
      confirmar: 'Confirmar',
      cancelar: 'Cancelar',
      destrutivo: false,
      ...base
    };

    return this.dialog
      .open<ConfirmDialogComponent, Required<OpcoesConfirmacao>, boolean>(ConfirmDialogComponent, {
        data: dados,
        autoFocus: 'first-tabbable', // respeita o cdkFocusInitial do botão Cancelar (ação segura)
        ariaLabelledBy: 'confirm-titulo',
        width: 'min(92vw, 420px)'
      })
      .afterClosed()
      .pipe(map(resultado => resultado === true));
  }
}
