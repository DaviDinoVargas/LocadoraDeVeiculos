import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ClientesService } from './clientes.service';
import { ClienteDto } from './cliente.model';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-clientes-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './clientes-list.component.html',
  styleUrls: ['../scss/global.scss']
})
export class ClientesListComponent implements OnInit {
  clientes: ClienteDto[] = [];
  clientesFiltrados: ClienteDto[] = [];
  loading = false;
  filtroTipo: string = 'todos';

  constructor(
    private svc: ClientesService,
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
        this.clientes = c;
        this.filtrarPorTipo(this.filtroTipo);
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar clientes:', err);
        this.snack.open('Erro ao listar clientes', 'Fechar', { duration: 4000 });
      }
    });
  }

  filtrarPorTipo(tipo: string) {
    this.filtroTipo = tipo;
    if (tipo === 'todos') {
      this.clientesFiltrados = [...this.clientes];
    } else {
      this.clientesFiltrados = this.clientes.filter(c => c.tipoCliente === tipo);
    }
  }

  obterDocumentoPrincipal(cliente: ClienteDto): string {
    if (cliente.tipoCliente === 'PessoaFisica') {
      return this.formatarCPF(cliente.cpf || '');
    } else {
      return this.formatarCNPJ(cliente.cnpj || '');
    }
  }

  formatarCPF(cpf: string): string {
    if (!cpf) return '';
    return cpf.replace(/(\d{3})(\d{3})(\d{3})(\d{2})/, '$1.$2.$3-$4');
  }

  formatarCNPJ(cnpj: string): string {
    if (!cnpj) return '';
    return cnpj.replace(/(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})/, '$1.$2.$3/$4-$5');
  }

  novo(tipo: 'PessoaFisica' | 'PessoaJuridica') {
    this.router.navigate([`/clientes/new/${tipo}`]);
  }

  editar(cliente: ClienteDto) {
    if (cliente.tipoCliente === 'PessoaFisica') {
      this.router.navigate([`/clientes/${cliente.id}/edit/pessoa-fisica`]);
    } else {
      this.router.navigate([`/clientes/${cliente.id}/edit/pessoa-juridica`]);
    }
  }

  excluir(id?: string) {
    if (!id || !confirm('Deseja realmente excluir este cliente?')) return;

    this.svc.excluir(id).subscribe({
      next: () => {
        this.snack.open('Cliente excluído', 'Fechar', { duration: 3000 });
        this.carregar();
      },
      error: (err) => {
        console.error('Erro ao excluir cliente:', err);
        this.snack.open('Falha ao excluir cliente', 'Fechar', { duration: 4000 });
      }
    });
  }
}
