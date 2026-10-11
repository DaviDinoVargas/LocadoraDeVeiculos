import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { crudLista } from '../../shared/data-table/crud-lista';
import { DataTableComponent } from '../../shared/data-table/data-table.component';
import { AcaoLinha, ColunaTabela } from '../../shared/data-table/data-table.types';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { mascararCnpj } from '../../shared/mascaras/mascara.directive';
import { CATEGORIAS_PARCEIRO, ParceiroDto, categoriaLabel } from './parceiro.model';
import { ParceirosService } from './parceiros.service';

@Component({
  selector: 'app-parceiros-list',
  imports: [RouterLink, NgIcon, HlmButtonImports, PageComponent, PageHeaderComponent, DataTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './parceiros-list.component.html'
})
export class ParceirosListComponent {
  private readonly svc = inject(ParceirosService);
  private readonly router = inject(Router);

  private readonly lista = crudLista<ParceiroDto>({
    listar: () => this.svc.listar(),
    excluir: id => this.svc.excluir(id),
    singular: 'parceiro'
  });
  readonly parceiros = this.lista.dados;
  readonly carregando = this.lista.carregando;
  readonly erro = this.lista.erro;
  readonly carregar = this.lista.carregar;
  readonly excluir = this.lista.excluir;
  readonly categoriaLabel = categoriaLabel;

  readonly colunas: ColunaTabela<ParceiroDto>[] = [
    { id: 'nome', cabecalho: 'Nome' },
    { id: 'cnpj', cabecalho: 'CNPJ', formatar: p => mascararCnpj(p.cnpj) },
    {
      id: 'categoria',
      cabecalho: 'Categoria',
      tipo: 'enum',
      opcoes: CATEGORIAS_PARCEIRO.map(c => ({ valor: c.value, rotulo: c.label, tom: 'info' as const }))
    },
    {
      id: 'ativo',
      cabecalho: 'Status',
      tipo: 'enum',
      valor: p => (p.ativo ? 'Ativo' : 'Inativo'),
      opcoes: [
        { valor: 'Ativo', rotulo: 'Ativo', tom: 'success' },
        { valor: 'Inativo', rotulo: 'Inativo', tom: 'neutral' }
      ]
    }
  ];

  readonly acoes: AcaoLinha<ParceiroDto>[] = [
    { rotulo: 'Editar', icone: 'lucidePencil', executar: p => this.editar(p.id) },
    { rotulo: 'Excluir', icone: 'lucideTrash2', destrutiva: true, executar: p => this.excluir(p.id) }
  ];

  readonly tituloLinha = (p: ParceiroDto) => p.nome;

  novo(): void {
    void this.router.navigate(['/parceiros/new']);
  }

  editar(id?: string): void {
    if (id) void this.router.navigate([`/parceiros/${id}/edit`]);
  }
}
