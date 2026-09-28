import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { CuponsService } from './cupons.service';
import { CupomDto, tipoDescontoLabel } from './cupom.model';

@Component({
  selector: 'app-cupons-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './cupons-list.component.html',
  styleUrls: ['../scss/global.scss']
})
export class CuponsListComponent implements OnInit {
  cupons: CupomDto[] = [];
  loading = false;
  tipoDescontoLabel = tipoDescontoLabel;

  constructor(
    private svc: CuponsService,
    private router: Router,
    private snack: MatSnackBar
  ) {}

  ngOnInit() {
    this.carregar();
  }

  carregar() {
    this.loading = true;
    this.svc.listar().subscribe({
      next: c => {
        this.cupons = c;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar cupons:', err);
        this.snack.open('Erro ao listar cupons', 'Fechar', { duration: 4000 });
      }
    });
  }

  formatarValor(cupom: CupomDto): string {
    if (cupom.tipoDesconto === 'Percentual') {
      return `${cupom.valorDesconto}%`;
    }
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(cupom.valorDesconto);
  }

  formatarData(dataString?: string): string {
    if (!dataString) return '';
    return new Date(dataString).toLocaleDateString('pt-BR');
  }

  novo() {
    this.router.navigate(['/cupons/new']);
  }

  editar(id?: string) {
    if (id) this.router.navigate([`/cupons/${id}/edit`]);
  }

  excluir(id?: string) {
    if (!id || !confirm('Deseja realmente excluir este cupom?')) return;

    this.svc.excluir(id).subscribe({
      next: () => {
        this.snack.open('Cupom excluído', 'Fechar', { duration: 3000 });
        this.carregar();
      },
      error: (err) => {
        console.error('Erro ao excluir cupom:', err);
        this.snack.open('Falha ao excluir cupom', 'Fechar', { duration: 4000 });
      }
    });
  }
}
