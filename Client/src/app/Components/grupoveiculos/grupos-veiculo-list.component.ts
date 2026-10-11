import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { GruposVeiculoService } from './grupos-veiculo.service';
import { GrupoVeiculoDto } from './grupo-veiculo.model';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { ConfirmacaoService } from '../../core/confirmacao.service';
import { filter } from 'rxjs/operators';

@Component({
  selector: 'app-grupos-veiculo-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './grupos-veiculo-list.component.html',
  styleUrls: ['../scss/global.scss']
})
export class GruposVeiculoListComponent {
  private readonly confirmacao = inject(ConfirmacaoService);

  grupos: GrupoVeiculoDto[] = [];
  loading = false;

  constructor(
    private svc: GruposVeiculoService,
    private router: Router,
    private snack: MatSnackBar
  ) {
    this.carregar();
  }

  carregar() {
    this.loading = true;
    this.svc.listar().subscribe({
      next: g => {
        this.grupos = g;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar grupos de veículo:', err);
        this.snack.open('Erro ao listar grupos de veículo', 'Fechar', { duration: 4000 });
      }
    });
  }

  novo() {
    this.router.navigate(['/grupoveiculos/new']);
  }

  editar(id?: string) {
    if (id) this.router.navigate([`/grupoveiculos/${id}/edit`]);
  }

  excluir(id?: string) {
    if (!id) return;

    this.confirmacao.confirmar({ titulo: 'Confirmar exclusão', mensagem: 'Deseja realmente excluir este grupo de veículo?', confirmar: 'Excluir', destrutivo: true })
      .pipe(filter(Boolean))
      .subscribe(() => {
        this.svc.excluir(id).subscribe({
          next: () => {
            this.snack.open('Grupo de veículo excluído', 'Fechar', { duration: 3000 });
            this.carregar();
          },
          error: (err) => {
            console.error('Erro ao excluir grupo de veículo:', err);
            this.snack.open('Falha ao excluir grupo de veículo', 'Fechar', { duration: 4000 });
          }
        });
      });
  }
}
