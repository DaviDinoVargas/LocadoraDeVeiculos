import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { FuncionariosService } from './funcionarios.service';
import { FuncionarioDto } from './funcionario.model';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-funcionarios-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './funcionarios-list.component.html',
  styleUrls: ['../scss/global.scss']
})
export class FuncionariosListComponent {
  funcionarios: FuncionarioDto[] = [];
  loading = false;

  // Para formatação
  today = new Date();

  constructor(
    private svc: FuncionariosService,
    private router: Router,
    private snack: MatSnackBar
  ) {
    this.carregar();
  }

  carregar() {
    this.loading = true;
    this.svc.listar().subscribe({
      next: f => {
        this.funcionarios = f;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar funcionários:', err);
        this.snack.open('Erro ao listar funcionários', 'Fechar', { duration: 4000 });
      }
    });
  }

  formatarCPF(cpf: string): string {
    if (!cpf) return '';
    // Formata CPF: 000.000.000-00
    return cpf.replace(/(\d{3})(\d{3})(\d{3})(\d{2})/, '$1.$2.$3-$4');
  }

  formatarData(dataString: string): string {
    if (!dataString) return '';
    const data = new Date(dataString);
    return data.toLocaleDateString('pt-BR');
  }

  formatarSalario(salario: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL'
    }).format(salario);
  }

  novo() {
    this.router.navigate(['/funcionarios/new']);
  }

  editar(id?: string) {
    if (id) this.router.navigate([`/funcionarios/${id}/edit`]);
  }

  excluir(id?: string) {
    if (!id || !confirm('Deseja realmente excluir este funcionário?')) return;

    this.svc.excluir(id).subscribe({
      next: () => {
        this.snack.open('Funcionário excluído', 'Fechar', { duration: 3000 });
        this.carregar();
      },
      error: (err) => {
        console.error('Erro ao excluir funcionário:', err);
        this.snack.open('Falha ao excluir funcionário', 'Fechar', { duration: 4000 });
      }
    });
  }
}
