import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { crudLista } from '../../shared/data-table/crud-lista';
import { DataTableComponent } from '../../shared/data-table/data-table.component';
import { AcaoLinha, ColunaTabela } from '../../shared/data-table/data-table.types';
import { formatarMoeda } from '../../shared/data-table/data-table.util';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { TaxaServicoDto } from './taxa-servico.model';
import { TaxasServicosService } from './taxas-servicos.service';

@Component({
  selector: 'app-taxas-servicos-list',
  imports: [RouterLink, NgIcon, HlmButtonImports, HlmNativeSelectImports, PageComponent, PageHeaderComponent, DataTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './taxas-servicos-list.component.html'
})
export class TaxasServicosListComponent {
  private readonly svc = inject(TaxasServicosService);
  private readonly router = inject(Router);

  // Tipo escolhido no filtro ('' = todos). A consulta é feita no servidor (GET /taxas-servicos/tipo/{tipo}).
  private tipoFiltro = '';

  private readonly lista = crudLista<TaxaServicoDto>({
    listar: () => (this.tipoFiltro ? this.svc.listarPorTipo(this.tipoFiltro) : this.svc.listar()),
    excluir: id => this.svc.excluir(id),
    singular: 'taxa ou serviço',
    genero: 'f'
  });
  readonly taxas = this.lista.dados;
  readonly carregando = this.lista.carregando;
  readonly erro = this.lista.erro;
  readonly carregar = this.lista.carregar;
  readonly excluir = this.lista.excluir;

  readonly colunas: ColunaTabela<TaxaServicoDto>[] = [
    { id: 'nome', cabecalho: 'Nome' },
    { id: 'preco', cabecalho: 'Preço', tipo: 'moeda' },
    {
      id: 'tipoCalculo',
      cabecalho: 'Tipo de cálculo',
      tipo: 'enum',
      opcoes: [
        { valor: 'Fixo', rotulo: 'Fixo', tom: 'info' },
        { valor: 'Diario', rotulo: 'Diário', tom: 'success' }
      ]
    }
  ];

  readonly acoes: AcaoLinha<TaxaServicoDto>[] = [
    { rotulo: 'Editar', icone: 'lucidePencil', executar: t => this.editar(t.id) },
    { rotulo: 'Excluir', icone: 'lucideTrash2', destrutiva: true, executar: t => this.excluir(t.id) }
  ];

  readonly tituloLinha = (t: TaxaServicoDto) => t.nome;

  filtrarPorTipo(tipo: string): void {
    this.tipoFiltro = tipo;
    this.carregar();
  }

  formatarMoeda(valor: number): string {
    return formatarMoeda(valor);
  }

  novo(): void {
    void this.router.navigate(['/taxas-servicos/new']);
  }

  editar(id?: string): void {
    if (id) void this.router.navigate([`/taxas-servicos/${id}/edit`]);
  }
}
