import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { AcaoLinha, ColunaTabela } from '../../shared/data-table/data-table.types';
import { DataTableComponent } from '../../shared/data-table/data-table.component';
import { crudLista } from '../../shared/data-table/crud-lista';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { GrupoVeiculoDto } from './grupo-veiculo.model';
import { GruposVeiculoService } from './grupos-veiculo.service';

@Component({
  selector: 'app-grupos-veiculo-list',
  imports: [RouterLink, NgIcon, HlmButtonImports, PageComponent, PageHeaderComponent, DataTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './grupos-veiculo-list.component.html'
})
export class GruposVeiculoListComponent {
  private readonly svc = inject(GruposVeiculoService);
  private readonly router = inject(Router);

  private readonly lista = crudLista<GrupoVeiculoDto>({
    listar: () => this.svc.listar(),
    excluir: id => this.svc.excluir(id),
    singular: 'grupo de veículo'
  });
  readonly grupos = this.lista.dados;
  readonly carregando = this.lista.carregando;
  readonly erro = this.lista.erro;
  readonly carregar = this.lista.carregar;
  readonly excluir = this.lista.excluir;

  readonly colunas: ColunaTabela<GrupoVeiculoDto>[] = [
    { id: 'nome', cabecalho: 'Nome' },
    { id: 'descricao', cabecalho: 'Descrição', truncar: 60 }
  ];

  readonly acoes: AcaoLinha<GrupoVeiculoDto>[] = [
    { rotulo: 'Editar', icone: 'lucidePencil', executar: g => this.editar(g.id) },
    { rotulo: 'Excluir', icone: 'lucideTrash2', destrutiva: true, executar: g => this.excluir(g.id) }
  ];

  readonly tituloLinha = (g: GrupoVeiculoDto) => g.nome;

  novo(): void {
    void this.router.navigate(['/grupoveiculos/new']);
  }

  editar(id?: string): void {
    if (id) void this.router.navigate([`/grupoveiculos/${id}/edit`]);
  }
}
