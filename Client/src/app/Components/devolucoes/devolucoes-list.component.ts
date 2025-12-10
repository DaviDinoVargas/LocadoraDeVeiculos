import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DevolucoesService } from './devolucoes.service';
import { SelecionarDevolucoesDto } from './devolucao.model';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-devolucoes-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './devolucoes-list.component.html',
  styleUrls: ['../scss/global.scss']
})
export class DevolucoesListComponent implements OnInit {
  devolucoes: SelecionarDevolucoesDto[] = [];
  loading = false;

  constructor(
    private svc: DevolucoesService,
    private router: Router,
    private snack: MatSnackBar
  ) {}

  ngOnInit() {
    this.carregar();
  }

  carregar() {
    this.loading = true;
    this.svc.listar().subscribe({
      next: d => {
        this.devolucoes = d;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar devoluções:', err);
        this.snack.open('Erro ao listar devoluções', 'Fechar', { duration: 4000 });
      }
    });
  }

  formatarData(dataString: string): string {
    if (!dataString) return '';
    const data = new Date(dataString);
    return data.toLocaleDateString('pt-BR');
  }

  formatarMoeda(valor: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL'
    }).format(valor);
  }

  visualizar(id?: string) {
    if (id) this.router.navigate([`/devolucoes/${id}`]);
  }

  excluir(id?: string) {
    if (!id || !confirm('Deseja realmente excluir esta devolução?')) return;

    this.svc.excluir(id).subscribe({
      next: () => {
        this.snack.open('Devolução excluída com sucesso', 'Fechar', { duration: 3000 });
        this.carregar();
      },
      error: (err) => {
        console.error('Erro ao excluir devolução:', err);
        this.snack.open('Falha ao excluir devolução', 'Fechar', { duration: 4000 });
      }
    });
  }
}
