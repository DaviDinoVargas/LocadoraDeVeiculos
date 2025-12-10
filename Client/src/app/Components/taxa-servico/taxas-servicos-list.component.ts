import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TaxasServicosService } from './taxas-servicos.service';
import { TaxaServicoDto } from './taxa-servico.model';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-taxas-servicos-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './taxas-servicos-list.component.html',
  styleUrls: ['../scss/global.scss']
})
export class TaxasServicosListComponent implements OnInit {
  taxas: TaxaServicoDto[] = [];
  taxasFiltradas: TaxaServicoDto[] = [];
  loading = false;

  constructor(
    private svc: TaxasServicosService,
    private router: Router,
    private snack: MatSnackBar
  ) {}

  ngOnInit() {
    this.carregar();
  }

  carregar() {
    this.loading = true;
    this.svc.listar().subscribe({
      next: t => {
        this.taxas = t;
        this.taxasFiltradas = [...t];
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar taxas e serviços:', err);
        this.snack.open('Erro ao listar taxas e serviços', 'Fechar', { duration: 4000 });
      }
    });
  }

  filtrarPorTipo(event: any) {
    const tipo = event.target.value;
    if (!tipo) {
      this.taxasFiltradas = [...this.taxas];
    } else {
      this.taxasFiltradas = this.taxas.filter(t => t.tipoCalculo === tipo);
    }
  }

  formatarMoeda(valor: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL'
    }).format(valor);
  }

  novo() {
    this.router.navigate(['/taxas-servicos/new']);
  }

  editar(id?: string) {
    if (id) this.router.navigate([`/taxas-servicos/${id}/edit`]);
  }

  excluir(id?: string) {
    if (!id || !confirm('Deseja realmente excluir esta taxa/serviço?')) return;

    this.svc.excluir(id).subscribe({
      next: () => {
        this.snack.open('Taxa/serviço excluído', 'Fechar', { duration: 3000 });
        this.carregar();
      },
      error: (err) => {
        console.error('Erro ao excluir taxa/serviço:', err);
        this.snack.open('Falha ao excluir taxa/serviço', 'Fechar', { duration: 4000 });
      }
    });
  }
}
