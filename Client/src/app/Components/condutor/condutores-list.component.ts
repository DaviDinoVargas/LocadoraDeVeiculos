import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CondutoresService } from './condutores.service';
import { ClientesService } from '../clientes/clientes.service';
import { CondutorDto } from './condutor.model';
import { ClienteDto } from '../clientes/cliente.model';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-condutores-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './condutores-list.component.html',
  styleUrls: ['../scss/global.scss']
})
export class CondutoresListComponent implements OnInit {
  condutores: CondutorDto[] = [];
  condutoresFiltrados: CondutorDto[] = [];
  clientes: ClienteDto[] = [];
  loading = false;

  constructor(
    private svc: CondutoresService,
    private clientesSvc: ClientesService,
    private router: Router,
    private snack: MatSnackBar
  ) {}

  ngOnInit() {
    this.carregarClientes();
    this.carregar();
  }

  carregarClientes() {
    this.clientesSvc.listar().subscribe({
      next: clientes => this.clientes = clientes,
      error: () => this.snack.open('Erro ao carregar clientes', 'Fechar', { duration: 4000 })
    });
  }

  carregar() {
    this.loading = true;
    this.svc.listar().subscribe({
      next: c => {
        this.condutores = c;
        this.condutoresFiltrados = [...c];
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar condutores:', err);
        this.snack.open('Erro ao listar condutores', 'Fechar', { duration: 4000 });
      }
    });
  }

  filtrarPorCliente(event: any) {
    const clienteId = event.target.value;
    if (!clienteId) {
      this.condutoresFiltrados = [...this.condutores];
    } else {
      this.condutoresFiltrados = this.condutores.filter(c => c.clienteId === clienteId);
    }
  }

  formatarCPF(cpf: string): string {
    if (!cpf) return '';
    return cpf.replace(/(\d{3})(\d{3})(\d{3})(\d{2})/, '$1.$2.$3-$4');
  }

  formatarData(dataString: string): string {
    if (!dataString) return '';
    const data = new Date(dataString);
    return data.toLocaleDateString('pt-BR');
  }

  novo() {
    this.router.navigate(['/condutores/new']);
  }

  editar(id?: string) {
    if (id) this.router.navigate([`/condutores/${id}/edit`]);
  }

  excluir(id?: string) {
    if (!id || !confirm('Deseja realmente excluir este condutor?')) return;

    this.svc.excluir(id).subscribe({
      next: () => {
        this.snack.open('Condutor excluído', 'Fechar', { duration: 3000 });
        this.carregar();
      },
      error: (err) => {
        console.error('Erro ao excluir condutor:', err);
        this.snack.open('Falha ao excluir condutor', 'Fechar', { duration: 4000 });
      }
    });
  }
}
