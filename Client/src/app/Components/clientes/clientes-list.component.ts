import { Component, OnInit } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ClientesService } from './clientes.service';
import { ClienteDto, ClientePessoaFisicaDto, ClientePessoaJuridicaDto } from './cliente.model';
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
  clientes: any[] = [];
  loading = false;
  modoLista: 'todos' | 'pf' | 'pj' = 'todos';
  titulo: string = 'Clientes';

  constructor(
    private svc: ClientesService,
    public router: Router, // Alterado para public para usar no template
    private route: ActivatedRoute,
    private snack: MatSnackBar
  ) {}

  ngOnInit() {
    // Detecta o modo da lista com base na URL
    const currentUrl = this.router.url;

    if (currentUrl.includes('/clientes/pf')) {
      this.modoLista = 'pf';
      this.titulo = 'Pessoas Físicas';
      this.carregarPessoasFisicas();
    } else if (currentUrl.includes('/clientes/pj')) {
      this.modoLista = 'pj';
      this.titulo = 'Pessoas Jurídicas';
      this.carregarPessoasJuridicas();
    } else {
      this.modoLista = 'todos';
      this.titulo = 'Clientes';
      this.carregarTodos();
    }
  }

  carregarTodos() {
    this.loading = true;
    this.svc.listar().subscribe({
      next: c => {
        this.clientes = c;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar clientes:', err);
        this.snack.open('Erro ao listar clientes', 'Fechar', { duration: 4000 });
      }
    });
  }

  carregarPessoasFisicas() {
    this.loading = true;
    this.svc.listarPessoasFisicas().subscribe({
      next: c => {
        this.clientes = c;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar pessoas físicas:', err);
        this.snack.open('Erro ao listar pessoas físicas', 'Fechar', { duration: 4000 });
      }
    });
  }

  carregarPessoasJuridicas() {
    this.loading = true;
    this.svc.listarPessoasJuridicas().subscribe({
      next: c => {
        this.clientes = c;
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar pessoas jurídicas:', err);
        this.snack.open('Erro ao listar pessoas jurídicas', 'Fechar', { duration: 4000 });
      }
    });
  }

  obterDocumentoPrincipal(cliente: any): string {
    if (this.modoLista === 'pf' || cliente.tipoCliente === 'PessoaFisica') {
      return this.formatarCPF(cliente.cpf || '');
    } else if (this.modoLista === 'pj' || cliente.tipoCliente === 'PessoaJuridica') {
      return this.formatarCNPJ(cliente.cnpj || '');
    }
    return '';
  }

  obterTipoCliente(cliente: any): string {
    if (this.modoLista === 'pf') return 'Pessoa Física';
    if (this.modoLista === 'pj') return 'Pessoa Jurídica';

    // Verifica se existe a propriedade tipoCliente (para ClienteDto)
    if (cliente.tipoCliente) {
      return cliente.tipoCliente === 'PessoaFisica' ? 'Pessoa Física' : 'Pessoa Jurídica';
    }

    // Se não tem tipoCliente, verifica por outras propriedades
    if (cliente.cpf) return 'Pessoa Física';
    if (cliente.cnpj) return 'Pessoa Jurídica';

    return 'Desconhecido';
  }

  formatarCPF(cpf: string): string {
    if (!cpf) return '';
    return cpf.replace(/(\d{3})(\d{3})(\d{3})(\d{2})/, '$1.$2.$3-$4');
  }

  formatarCNPJ(cnpj: string): string {
    if (!cnpj) return '';
    return cnpj.replace(/(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})/, '$1.$2.$3/$4-$5');
  }

  filtrarPorTipo(tipo: string) {
    if (this.modoLista === 'todos') {
      // Só aplica filtro se estiver na lista geral
      if (tipo === 'todos') {
        this.carregarTodos();
      } else {
        this.carregarTodosComFiltro(tipo);
      }
    } else {
      // Se estiver em uma lista específica, navega para a lista geral com filtro
      this.router.navigate(['/clientes']);
    }
  }

  private carregarTodosComFiltro(tipo: string) {
    this.loading = true;
    this.svc.listar().subscribe({
      next: c => {
        this.clientes = c.filter(cliente => {
          // Para ClienteDto com tipoCliente
          if (cliente.tipoCliente) {
            return cliente.tipoCliente === tipo;
          }
          // Para outros tipos, inferir pelo documento
          if (tipo === 'PessoaFisica' && cliente.cpf) return true;
          if (tipo === 'PessoaJuridica' && cliente.cnpj) return true;
          return false;
        });
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        console.error('Erro ao listar clientes:', err);
        this.snack.open('Erro ao listar clientes', 'Fechar', { duration: 4000 });
      }
    });
  }

  novo() {
    if (this.modoLista === 'pf') {
      this.router.navigate(['/clientes/new/PessoaFisica']);
    } else if (this.modoLista === 'pj') {
      this.router.navigate(['/clientes/new/PessoaJuridica']);
    }
  }

  novoCliente(tipo: 'pf' | 'pj') {
  this.router.navigate([`/clientes/new/${tipo}`]);
}

  editar(cliente: any) {
    if (this.modoLista === 'pf' || cliente.tipoCliente === 'PessoaFisica' || cliente.cpf) {
      this.router.navigate([`/clientes/${cliente.id}/edit/pessoa-fisica`]);
    } else if (this.modoLista === 'pj' || cliente.tipoCliente === 'PessoaJuridica' || cliente.cnpj) {
      this.router.navigate([`/clientes/${cliente.id}/edit/pessoa-juridica`]);
    }
  }

  excluir(id?: string) {
    if (!id || !confirm('Deseja realmente excluir este cliente?')) return;

    this.svc.excluir(id).subscribe({
      next: () => {
        this.snack.open('Cliente excluído', 'Fechar', { duration: 3000 });
        // Recarrega a lista apropriada
        if (this.modoLista === 'pf') {
          this.carregarPessoasFisicas();
        } else if (this.modoLista === 'pj') {
          this.carregarPessoasJuridicas();
        } else {
          this.carregarTodos();
        }
      },
      error: (err) => {
        console.error('Erro ao excluir cliente:', err);
        this.snack.open('Falha ao excluir cliente', 'Fechar', { duration: 4000 });
      }
    });
  }

  navegarParaPf() {
    this.router.navigate(['/clientes/pf']);
  }

  navegarParaPj() {
    this.router.navigate(['/clientes/pj']);
  }
}
