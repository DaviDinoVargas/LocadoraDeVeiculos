import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { ParceirosService } from './parceiros.service';
import { ParceiroDto, categoriaLabel } from './parceiro.model';

@Component({
  selector: 'app-parceiros-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './parceiros-list.component.html',
  styleUrls: ['../scss/global.scss']
})
export class ParceirosListComponent implements OnInit {
  parceiros: ParceiroDto[] = [];
  loading = false;
  categoriaLabel = categoriaLabel;

  constructor(
    private svc: ParceirosService,
    private router: Router,
    private snack: MatSnackBar
  ) {}

  ngOnInit() {
    this.carregar();
  }

  carregar() {
    this.loading = true;
    this.svc.listar().subscribe({
      next: p => {
        this.parceiros = p;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar parceiros:', err);
        this.snack.open('Erro ao listar parceiros', 'Fechar', { duration: 4000 });
      }
    });
  }

  novo() {
    this.router.navigate(['/parceiros/new']);
  }

  editar(id?: string) {
    if (id) this.router.navigate([`/parceiros/${id}/edit`]);
  }

  excluir(id?: string) {
    if (!id || !confirm('Deseja realmente excluir este parceiro?')) return;

    this.svc.excluir(id).subscribe({
      next: () => {
        this.snack.open('Parceiro excluído', 'Fechar', { duration: 3000 });
        this.carregar();
      },
      error: (err) => {
        console.error('Erro ao excluir parceiro:', err);
        this.snack.open('Falha ao excluir parceiro', 'Fechar', { duration: 4000 });
      }
    });
  }
}
