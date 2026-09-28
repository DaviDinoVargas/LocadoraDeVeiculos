import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { DesafiosCupomService } from './desafios-cupom.service';
import { DesafioCupomDto } from './desafio-cupom.model';

@Component({
  selector: 'app-desafios-cupom-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './desafios-cupom-list.component.html',
  styleUrls: ['../scss/global.scss']
})
export class DesafiosCupomListComponent implements OnInit {
  desafios: DesafioCupomDto[] = [];
  loading = false;

  constructor(
    private svc: DesafiosCupomService,
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
        this.desafios = d;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar desafios de cupom:', err);
        this.snack.open('Erro ao listar desafios de cupom', 'Fechar', { duration: 4000 });
      }
    });
  }

  novo() {
    this.router.navigate(['/desafios-cupom/new']);
  }

  editar(id?: string) {
    if (id) this.router.navigate([`/desafios-cupom/${id}/edit`]);
  }

  excluir(id?: string) {
    if (!id || !confirm('Deseja realmente excluir este desafio?')) return;

    this.svc.excluir(id).subscribe({
      next: () => {
        this.snack.open('Desafio excluído', 'Fechar', { duration: 3000 });
        this.carregar();
      },
      error: (err) => {
        console.error('Erro ao excluir desafio:', err);
        this.snack.open('Falha ao excluir desafio', 'Fechar', { duration: 4000 });
      }
    });
  }
}
