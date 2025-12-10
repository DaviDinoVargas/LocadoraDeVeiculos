import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { GruposVeiculoService } from './grupos-veiculo.service';
import { GrupoVeiculoDto } from './grupo-veiculo.model';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-grupos-veiculo-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './grupos-veiculo-list.component.html',
  styleUrls: ['../scss/global.scss']
})
export class GruposVeiculoListComponent {
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
    if (!id || !confirm('Deseja realmente excluir este grupo de veículo?')) return;

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
  }
}
