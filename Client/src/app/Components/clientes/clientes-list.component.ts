import { ChangeDetectionStrategy, Component, OnInit, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmDropdownMenuImports } from '@spartan-ng/helm/dropdown-menu';
import { crudLista } from '../../shared/data-table/crud-lista';
import { DataTableComponent } from '../../shared/data-table/data-table.component';
import { AcaoLinha, ColunaTabela } from '../../shared/data-table/data-table.types';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { mascararCnpj, mascararCpf } from '../../shared/mascaras/mascara.directive';
import { ClienteDto } from './cliente.model';
import { ClientesService } from './clientes.service';

type ModoLista = 'todos' | 'pf' | 'pj';

@Component({
  selector: 'app-clientes-list',
  imports: [RouterLink, RouterLinkActive, NgIcon, HlmButtonImports, HlmDropdownMenuImports, PageComponent, PageHeaderComponent, DataTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './clientes-list.component.html'
})
export class ClientesListComponent implements OnInit {
  private readonly svc = inject(ClientesService);
  readonly router = inject(Router);

  // O modo (todos / pessoas físicas / pessoas jurídicas) vem da URL: cada um tem o seu endpoint.
  modoLista: ModoLista = 'todos';
  titulo = 'Clientes';

  private readonly lista = crudLista<ClienteDto>({
    listar: () => {
      if (this.modoLista === 'pf') return this.svc.listarPessoasFisicas();
      if (this.modoLista === 'pj') return this.svc.listarPessoasJuridicas();
      return this.svc.listar();
    },
    excluir: id => this.svc.excluir(id),
    singular: 'cliente',
    carregarAoCriar: false
  });
  readonly clientes = this.lista.dados;
  readonly carregando = this.lista.carregando;
  readonly erro = this.lista.erro;
  readonly carregar = this.lista.carregar;
  readonly excluir = this.lista.excluir;

  readonly colunas = computed<ColunaTabela<ClienteDto>[]>(() => {
    const base: ColunaTabela<ClienteDto>[] = [
      { id: 'nome', cabecalho: 'Nome' },
      { id: 'documento', cabecalho: 'Documento', valor: c => this.obterDocumentoPrincipal(c) },
      { id: 'telefone', cabecalho: 'Telefone' },
      { id: 'email', cabecalho: 'E-mail', truncar: 30, prioridade: 'baixa' }
    ];
    if (this.modoLista !== 'todos') return base;
    return [
      ...base,
      {
        id: 'tipo',
        cabecalho: 'Tipo',
        tipo: 'enum',
        valor: c => this.obterTipoCliente(c),
        opcoes: [
          { valor: 'Pessoa Física', rotulo: 'Pessoa física', tom: 'info', },
          { valor: 'Pessoa Jurídica', rotulo: 'Pessoa jurídica', tom: 'warning' }
        ]
      }
    ];
  });

  readonly acoes: AcaoLinha<ClienteDto>[] = [
    { rotulo: 'Editar', icone: 'lucidePencil', executar: c => this.editar(c) },
    { rotulo: 'Excluir', icone: 'lucideTrash2', destrutiva: true, executar: c => this.excluir(c.id) }
  ];

  readonly tituloLinha = (c: ClienteDto) => c.nome;

  ngOnInit(): void {
    // Detecta o modo da lista com base na URL
    const urlAtual = this.router.url;
    if (urlAtual.includes('/clientes/pf')) {
      this.modoLista = 'pf';
      this.titulo = 'Pessoas físicas';
    } else if (urlAtual.includes('/clientes/pj')) {
      this.modoLista = 'pj';
      this.titulo = 'Pessoas jurídicas';
    }
    this.carregar();
  }

  // A lista geral (GET /clientes) devolve só `documentoPrincipal`; cpf/cnpj só existem nas listas
  // pf/pj e no detalhe. Antes a coluna "Documento" ficava vazia na aba "Todos".
  obterDocumentoPrincipal(cliente: Partial<ClienteDto>): string {
    const documento: string = cliente.documentoPrincipal || cliente.cpf || cliente.cnpj || '';
    const digitos = documento.replace(/\D/g, '');

    if (digitos.length === 11) return mascararCpf(digitos);
    if (digitos.length === 14) return mascararCnpj(digitos);
    return documento || '—';
  }

  obterTipoCliente(cliente: Partial<ClienteDto>): string {
    if (this.modoLista === 'pf') return 'Pessoa Física';
    if (this.modoLista === 'pj') return 'Pessoa Jurídica';

    if (cliente.tipoCliente) {
      return cliente.tipoCliente === 'PessoaFisica' ? 'Pessoa Física' : 'Pessoa Jurídica';
    }

    // Sem tipoCliente, deduz pelo documento
    if (cliente.cpf) return 'Pessoa Física';
    if (cliente.cnpj) return 'Pessoa Jurídica';

    return 'Desconhecido';
  }

  novo(): void {
    if (this.modoLista === 'pf') {
      void this.router.navigate(['/clientes/new/pf']);
    } else if (this.modoLista === 'pj') {
      void this.router.navigate(['/clientes/new/pj']);
    }
  }

  novoCliente(tipo: 'pf' | 'pj'): void {
    void this.router.navigate([`/clientes/new/${tipo}`]);
  }

  editar(cliente: ClienteDto): void {
    if (this.modoLista === 'pf' || cliente.tipoCliente === 'PessoaFisica' || cliente.cpf) {
      void this.router.navigate([`/clientes/${cliente.id}/edit/pessoa-fisica`]);
    } else if (this.modoLista === 'pj' || cliente.tipoCliente === 'PessoaJuridica' || cliente.cnpj) {
      void this.router.navigate([`/clientes/${cliente.id}/edit/pessoa-juridica`]);
    }
  }
}
