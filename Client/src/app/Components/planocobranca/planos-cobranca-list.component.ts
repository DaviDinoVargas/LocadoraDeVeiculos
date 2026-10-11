import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { crudLista } from '../../shared/data-table/crud-lista';
import { DataTableComponent } from '../../shared/data-table/data-table.component';
import { AcaoLinha, ColunaTabela } from '../../shared/data-table/data-table.types';
import { formatarMoeda } from '../../shared/data-table/data-table.util';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { PlanoCobrancaDto } from './plano-cobranca.model';
import { PlanosCobrancaService } from './planos-cobranca.service';

@Component({
  selector: 'app-planos-cobranca-list',
  imports: [RouterLink, NgIcon, HlmButtonImports, PageComponent, PageHeaderComponent, DataTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './planos-cobranca-list.component.html'
})
export class PlanosCobrancaListComponent {
  private readonly svc = inject(PlanosCobrancaService);
  private readonly router = inject(Router);

  private readonly lista = crudLista<PlanoCobrancaDto>({
    listar: () => this.svc.listar(),
    excluir: id => this.svc.excluir(id),
    singular: 'plano de cobrança'
  });
  readonly planos = this.lista.dados;
  readonly carregando = this.lista.carregando;
  readonly erro = this.lista.erro;
  readonly carregar = this.lista.carregar;
  readonly excluir = this.lista.excluir;

  readonly colunas: ColunaTabela<PlanoCobrancaDto>[] = [
    { id: 'nome', cabecalho: 'Nome' },
    { id: 'grupo', cabecalho: 'Grupo de veículo', valor: p => p.grupoAutomovelNome, filtravel: true },
    { id: 'precoDiaria', cabecalho: 'Diária', tipo: 'moeda' },
    { id: 'precoPorKm', cabecalho: 'Preço por km', tipo: 'moeda', formatar: p => (p.precoPorKm ? formatarMoeda(p.precoPorKm) : '—') },
    { id: 'kmLivreLimite', cabecalho: 'Km livre', tipo: 'numero', formatar: p => (p.kmLivreLimite != null ? String(p.kmLivreLimite) : '—') }
  ];

  readonly acoes: AcaoLinha<PlanoCobrancaDto>[] = [
    { rotulo: 'Editar', icone: 'lucidePencil', executar: p => this.editar(p.id) },
    { rotulo: 'Excluir', icone: 'lucideTrash2', destrutiva: true, executar: p => this.excluir(p.id) }
  ];

  readonly tituloLinha = (p: PlanoCobrancaDto) => p.nome;

  formatarMoeda(valor: number): string {
    return formatarMoeda(valor);
  }

  novo(): void {
    void this.router.navigate(['/planos-cobranca/new']);
  }

  editar(id?: string): void {
    if (id) void this.router.navigate([`/planos-cobranca/${id}/edit`]);
  }
}
