import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { crudLista } from '../../shared/data-table/crud-lista';
import { DataTableComponent } from '../../shared/data-table/data-table.component';
import { AcaoLinha, ColunaTabela } from '../../shared/data-table/data-table.types';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { DesafioCupomDto } from './desafio-cupom.model';
import { DesafiosCupomService } from './desafios-cupom.service';

@Component({
  selector: 'app-desafios-cupom-list',
  imports: [RouterLink, NgIcon, HlmButtonImports, PageComponent, PageHeaderComponent, DataTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './desafios-cupom-list.component.html'
})
export class DesafiosCupomListComponent {
  private readonly svc = inject(DesafiosCupomService);
  private readonly router = inject(Router);

  private readonly lista = crudLista<DesafioCupomDto>({
    listar: () => this.svc.listar(),
    excluir: id => this.svc.excluir(id),
    singular: 'desafio'
  });
  readonly desafios = this.lista.dados;
  readonly carregando = this.lista.carregando;
  readonly erro = this.lista.erro;
  readonly carregar = this.lista.carregar;
  readonly excluir = this.lista.excluir;

  readonly colunas: ColunaTabela<DesafioCupomDto>[] = [
    { id: 'nome', cabecalho: 'Nome' },
    { id: 'descricao', cabecalho: 'Descrição', truncar: 36, prioridade: 'baixa' },
    { id: 'metaQuantidadeAlugueis', cabecalho: 'Meta', tipo: 'numero', formatar: d => `${d.metaQuantidadeAlugueis} aluguéis` },
    { id: 'periodoDias', cabecalho: 'Período', tipo: 'numero', formatar: d => `${d.periodoDias} dias` },
    { id: 'cupomRecompensaCodigo', cabecalho: 'Cupom de recompensa' },
    {
      id: 'ativo',
      cabecalho: 'Status',
      tipo: 'enum',
      valor: d => (d.ativo ? 'Ativo' : 'Inativo'),
      opcoes: [
        { valor: 'Ativo', rotulo: 'Ativo', tom: 'success' },
        { valor: 'Inativo', rotulo: 'Inativo', tom: 'neutral' }
      ]
    }
  ];

  readonly acoes: AcaoLinha<DesafioCupomDto>[] = [
    { rotulo: 'Editar', icone: 'lucidePencil', executar: d => this.editar(d.id) },
    { rotulo: 'Excluir', icone: 'lucideTrash2', destrutiva: true, executar: d => this.excluir(d.id) }
  ];

  readonly tituloLinha = (d: DesafioCupomDto) => d.nome;

  novo(): void {
    void this.router.navigate(['/desafios-cupom/new']);
  }

  editar(id?: string): void {
    if (id) void this.router.navigate([`/desafios-cupom/${id}/edit`]);
  }
}
