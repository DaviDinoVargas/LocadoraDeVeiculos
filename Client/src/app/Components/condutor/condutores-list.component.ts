import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { crudLista } from '../../shared/data-table/crud-lista';
import { DataTableComponent } from '../../shared/data-table/data-table.component';
import { AcaoLinha, ColunaTabela } from '../../shared/data-table/data-table.types';
import { formatarData } from '../../shared/data-table/data-table.util';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { mascararCpf } from '../../shared/mascaras/mascara.directive';
import { ClienteDto } from '../clientes/cliente.model';
import { ClientesService } from '../clientes/clientes.service';
import { CondutorDto } from './condutor.model';
import { CondutoresService } from './condutores.service';

@Component({
  selector: 'app-condutores-list',
  imports: [RouterLink, NgIcon, HlmButtonImports, HlmNativeSelectImports, PageComponent, PageHeaderComponent, DataTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './condutores-list.component.html'
})
export class CondutoresListComponent implements OnInit {
  private readonly svc = inject(CondutoresService);
  private readonly clientesSvc = inject(ClientesService);
  private readonly router = inject(Router);

  private readonly lista = crudLista<CondutorDto>({
    listar: () => this.svc.listar(),
    excluir: id => this.svc.excluir(id),
    singular: 'condutor',
    carregarAoCriar: false
  });
  readonly condutores = this.lista.dados;
  readonly carregando = this.lista.carregando;
  readonly erro = this.lista.erro;
  readonly carregar = this.lista.carregar;
  readonly excluir = this.lista.excluir;

  readonly clientes = signal<ClienteDto[]>([]);
  private readonly clienteFiltroId = signal('');
  readonly condutoresFiltrados = computed(() => {
    const clienteId = this.clienteFiltroId();
    return clienteId ? this.condutores().filter(c => c.clienteId === clienteId) : this.condutores();
  });

  readonly colunas: ColunaTabela<CondutorDto>[] = [
    { id: 'nome', cabecalho: 'Nome' },
    { id: 'cpf', cabecalho: 'CPF', formatar: c => this.formatarCPF(c.cpf), prioridade: 'baixa' },
    { id: 'cnh', cabecalho: 'CNH' },
    { id: 'validadeCnh', cabecalho: 'Validade da CNH', tipo: 'data' },
    {
      id: 'situacaoCnh',
      cabecalho: 'Situação da CNH',
      tipo: 'enum',
      valor: c => (this.cnhVencida(c) ? 'Vencida' : 'EmDia'),
      opcoes: [
        { valor: 'EmDia', rotulo: 'Em dia', tom: 'success' },
        { valor: 'Vencida', rotulo: 'Vencida', tom: 'error' }
      ]
    },
    { id: 'telefone', cabecalho: 'Telefone', prioridade: 'baixa' },
    { id: 'email', cabecalho: 'E-mail', truncar: 28, prioridade: 'baixa' },
    { id: 'clienteNome', cabecalho: 'Cliente' }
  ];

  readonly acoes: AcaoLinha<CondutorDto>[] = [
    { rotulo: 'Editar', icone: 'lucidePencil', executar: c => this.editar(c.id) },
    { rotulo: 'Excluir', icone: 'lucideTrash2', destrutiva: true, executar: c => this.excluir(c.id) }
  ];

  readonly tituloLinha = (c: CondutorDto) => c.nome;

  ngOnInit(): void {
    this.clientesSvc.listar().subscribe({ next: clientes => this.clientes.set(clientes) });
    this.carregar();
  }

  filtrarPorCliente(clienteId: string): void {
    this.clienteFiltroId.set(clienteId);
  }

  cnhVencida(condutor: CondutorDto): boolean {
    const validade = new Date(condutor.validadeCnh);
    return !Number.isNaN(validade.getTime()) && validade.getTime() < new Date().setHours(0, 0, 0, 0);
  }

  formatarCPF(cpf: string): string {
    return mascararCpf(cpf);
  }

  formatarData(dataString: string): string {
    return formatarData(dataString);
  }

  novo(): void {
    void this.router.navigate(['/condutores/new']);
  }

  editar(id?: string): void {
    if (id) void this.router.navigate([`/condutores/${id}/edit`]);
  }
}
