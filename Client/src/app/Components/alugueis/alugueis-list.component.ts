import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AlugueisService } from './alugueis.service';
import { DevolucoesService } from '../devolucoes/devolucoes.service';
import { PdfService } from '../pdf/pdf.service';
import { SelecionarAlugueisDto } from './aluguel.model';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-alugueis-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './alugueis-list.component.html',
  styleUrls: ['../scss/global.scss']
})
export class AlugueisListComponent implements OnInit {
  alugueisFiltrados: SelecionarAlugueisDto[] = [];
  loading = false;
  filtroStatus: string = 'todos';

  constructor(
    private svc: AlugueisService,
    private devolucoesSvc: DevolucoesService,
    private pdf: PdfService,
    private router: Router,
    private route: ActivatedRoute,
    private snack: MatSnackBar
  ) {}

  ngOnInit() {
    const statusInicial = this.route.snapshot.queryParamMap.get('status');
    if (statusInicial) {
      this.filtroStatus = statusInicial;
    }
    this.carregar();
  }

  carregar() {
    this.filtrarPorStatus(this.filtroStatus);
  }

  // O filtro é feito no servidor: todos -> GET /alugueis, em-aberto -> /alugueis/em-aberto e
  // cada status -> /alugueis/status/{status} (antes carregava tudo e filtrava na memória).
  filtrarPorStatus(status: string) {
    this.filtroStatus = status;
    this.loading = true;

    const origem$ = status === 'todos' ? this.svc.listar()
      : status === 'em-aberto' ? this.svc.selecionarEmAberto()
      : this.svc.listarPorStatus(status);

    origem$.subscribe({
      next: a => {
        this.alugueisFiltrados = a;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar aluguéis:', err);
        this.snack.open('Erro ao listar aluguéis', 'Fechar', { duration: 4000 });
      }
    });
  }

  private readonly nomesDosFiltros: { [status: string]: string } = {
    todos: 'Todos os aluguéis', 'em-aberto': 'Em aberto', Reservado: 'Reservados',
    EmAndamento: 'Em andamento', Concluido: 'Concluídos', Cancelado: 'Cancelados'
  };

  /** Exporta em PDF exatamente a lista que está na tela (já filtrada por status). */
  exportarPdf() {
    if (this.alugueisFiltrados.length === 0) return;
    this.pdf.gerarRelatorioAlugueis(this.alugueisFiltrados, this.nomesDosFiltros[this.filtroStatus] ?? this.filtroStatus);
  }

  // Aluguel concluído: leva à devolução registrada (GET /devolucoes/aluguel/{id}).
  verDevolucao(aluguelId?: string) {
    if (!aluguelId) return;

    this.devolucoesSvc.obterPorAluguelId(aluguelId).subscribe({
      next: d => this.router.navigate([`/devolucoes/${d.id}`]),
      error: () => this.snack.open('Este aluguel ainda não tem devolução registrada.', 'Fechar', { duration: 4000 })
    });
  }

  getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'Reservado': return 'bg-info';
      case 'EmAndamento': return 'bg-primary';
      case 'Concluido': return 'bg-success';
      case 'Cancelado': return 'bg-danger';
      default: return 'bg-secondary';
    }
  }

  getStatusText(status: string): string {
    switch (status) {
      case 'Reservado': return 'Reservado';
      case 'EmAndamento': return 'Em Andamento';
      case 'Concluido': return 'Concluído';
      case 'Cancelado': return 'Cancelado';
      default: return status;
    }
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

  novo() {
    this.router.navigate(['/alugueis/new']);
  }

  editar(id?: string) {
    if (id) this.router.navigate([`/alugueis/${id}/edit`]);
  }

  devolver(id?: string) {
  if (id) this.router.navigate([`/alugueis/${id}/devolver`]);
}

  iniciar(id?: string) {
    if (!id || !confirm('Deseja realmente iniciar este aluguel?')) return;

    this.svc.iniciar(id).subscribe({
      next: () => {
        this.snack.open('Aluguel iniciado com sucesso', 'Fechar', { duration: 3000 });
        this.carregar();
      },
      error: (err) => {
        console.error('Erro ao iniciar aluguel:', err);
        this.snack.open('Falha ao iniciar aluguel', 'Fechar', { duration: 4000 });
      }
    });
  }

  cancelar(id?: string) {
    if (!id || !confirm('Deseja realmente cancelar este aluguel?')) return;

    this.svc.cancelar(id).subscribe({
      next: () => {
        this.snack.open('Aluguel cancelado com sucesso', 'Fechar', { duration: 3000 });
        this.carregar();
      },
      error: (err) => {
        console.error('Erro ao cancelar aluguel:', err);
        this.snack.open('Falha ao cancelar aluguel', 'Fechar', { duration: 4000 });
      }
    });
  }

  excluir(id?: string) {
    if (!id || !confirm('Deseja realmente excluir este aluguel?')) return;

    this.svc.excluir(id).subscribe({
      next: () => {
        this.snack.open('Aluguel excluído com sucesso', 'Fechar', { duration: 3000 });
        this.carregar();
      },
      error: (err) => {
        console.error('Erro ao excluir aluguel:', err);
        this.snack.open('Falha ao excluir aluguel', 'Fechar', { duration: 4000 });
      }
    });
  }
}
