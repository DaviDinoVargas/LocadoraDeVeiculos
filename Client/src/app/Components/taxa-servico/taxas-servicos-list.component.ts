import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TaxasServicosService } from './taxas-servicos.service';
import { TaxaServicoDto } from './taxa-servico.model';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { ConfirmacaoService } from '../../core/confirmacao.service';
import { filter } from 'rxjs/operators';

@Component({
  selector: 'app-taxas-servicos-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './taxas-servicos-list.component.html',
  styleUrls: ['../scss/global.scss']
})
export class TaxasServicosListComponent implements OnInit {
  private readonly confirmacao = inject(ConfirmacaoService);

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

  // Filtro no servidor (GET /taxas-servicos/tipo/{tipo}); "todos" reaproveita a lista já carregada.
  filtrarPorTipo(event: Event) {
    const tipo = (event.target as HTMLSelectElement).value;
    if (!tipo) {
      this.taxasFiltradas = [...this.taxas];
      return;
    }

    this.loading = true;
    this.svc.listarPorTipo(tipo).subscribe({
      next: t => {
        this.taxasFiltradas = t;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao filtrar taxas e serviços:', err);
        this.snack.open('Erro ao filtrar taxas e serviços', 'Fechar', { duration: 4000 });
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
    this.router.navigate(['/taxas-servicos/new']);
  }

  editar(id?: string) {
    if (id) this.router.navigate([`/taxas-servicos/${id}/edit`]);
  }

  excluir(id?: string) {
    if (!id) return;

    this.confirmacao.confirmar({ titulo: 'Confirmar exclusão', mensagem: 'Deseja realmente excluir esta taxa/serviço?', confirmar: 'Excluir', destrutivo: true })
      .pipe(filter(Boolean))
      .subscribe(() => {
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
      });
  }
}
