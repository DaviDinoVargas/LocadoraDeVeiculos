import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PlanosCobrancaService } from './planos-cobranca.service';
import { PlanoCobrancaDto } from './plano-cobranca.model';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-planos-cobranca-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './planos-cobranca-list.component.html',
  styleUrls: ['../scss/global.scss']
})
export class PlanosCobrancaListComponent {
  planos: PlanoCobrancaDto[] = [];
  loading = false;

  constructor(
    private svc: PlanosCobrancaService,
    private router: Router,
    private snack: MatSnackBar
  ) {
    this.carregar();
  }

  carregar() {
    this.loading = true;
    this.svc.listar().subscribe({
      next: p => {
        this.planos = p;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar planos de cobrança:', err);
        this.snack.open('Erro ao listar planos de cobrança', 'Fechar', { duration: 4000 });
      }
    });
  }

  formatarMoeda(valor: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL'
    }).format(valor);
  }

  novo() {
    this.router.navigate(['/planos-cobranca/new']);
  }

  editar(id?: string) {
    if (id) this.router.navigate([`/planos-cobranca/${id}/edit`]);
  }

  excluir(id?: string) {
    if (!id || !confirm('Deseja realmente excluir este plano de cobrança?')) return;

    this.svc.excluir(id).subscribe({
      next: () => {
        this.snack.open('Plano de cobrança excluído', 'Fechar', { duration: 3000 });
        this.carregar();
      },
      error: (err) => {
        console.error('Erro ao excluir plano de cobrança:', err);
        this.snack.open('Falha ao excluir plano de cobrança', 'Fechar', { duration: 4000 });
      }
    });
  }
}
