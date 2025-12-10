import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AlugueisService } from './alugueis.service';
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
  alugueis: SelecionarAlugueisDto[] = [];
  alugueisFiltrados: SelecionarAlugueisDto[] = [];
  loading = false;
  filtroStatus: string = 'todos';

  constructor(
    private svc: AlugueisService,
    private router: Router,
    private snack: MatSnackBar
  ) {}

  ngOnInit() {
    this.carregar();
  }

  carregar() {
    this.loading = true;
    this.svc.listar().subscribe({
      next: a => {
        this.alugueis = a;
        this.filtrarPorStatus(this.filtroStatus);
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar aluguéis:', err);
        this.snack.open('Erro ao listar aluguéis', 'Fechar', { duration: 4000 });
      }
    });
  }

  filtrarPorStatus(status: string) {
    this.filtroStatus = status;
    if (status === 'todos') {
      this.alugueisFiltrados = [...this.alugueis];
    } else if (status === 'em-aberto') {
      this.carregarEmAberto();
    } else {
      this.alugueisFiltrados = this.alugueis.filter(a => a.status === status);
    }
  }

  carregarEmAberto() {
    this.loading = true;
    this.svc.listarEmAberto().subscribe({
      next: a => {
        this.alugueisFiltrados = a;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar aluguéis em aberto:', err);
        this.snack.open('Erro ao listar aluguéis em aberto', 'Fechar', { duration: 4000 });
      }
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
