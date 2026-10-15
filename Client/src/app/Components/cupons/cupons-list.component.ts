import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { crudLista } from '../../shared/data-table/crud-lista';
import { DataTableComponent } from '../../shared/data-table/data-table.component';
import { AcaoLinha, ColunaTabela } from '../../shared/data-table/data-table.types';
import { formatarData, formatarMoeda } from '../../shared/data-table/data-table.util';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { CupomDto, tipoDescontoLabel } from './cupom.model';
import { CuponsService } from './cupons.service';

@Component({
  selector: 'app-cupons-list',
  imports: [RouterLink, NgIcon, HlmButtonImports, PageComponent, PageHeaderComponent, DataTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cupons-list.component.html'
})
export class CuponsListComponent {
  private readonly svc = inject(CuponsService);
  private readonly router = inject(Router);

  private readonly lista = crudLista<CupomDto>({
    listar: () => this.svc.listar(),
    excluir: id => this.svc.excluir(id),
    singular: 'cupom'
  });
  readonly cupons = this.lista.dados;
  readonly carregando = this.lista.carregando;
  readonly erro = this.lista.erro;
  readonly carregar = this.lista.carregar;
  readonly excluir = this.lista.excluir;
  readonly tipoDescontoLabel = tipoDescontoLabel;

  readonly colunas: ColunaTabela<CupomDto>[] = [
    { id: 'codigo', cabecalho: 'Código' },
    { id: 'descricao', cabecalho: 'Descrição', truncar: 34, prioridade: 'baixa' },
    { id: 'valorDesconto', cabecalho: 'Desconto', tipo: 'numero', formatar: c => this.formatarValor(c) },
    { id: 'validoAte', cabecalho: 'Válido até', tipo: 'data' },
    { id: 'usos', cabecalho: 'Usos', tipo: 'numero', valor: c => c.usosAtuais ?? 0, formatar: c => this.formatarUsos(c) },
    { id: 'parceiroNome', cabecalho: 'Parceiro', filtravel: true },
    {
      id: 'ativo',
      cabecalho: 'Status',
      tipo: 'enum',
      valor: c => (c.ativo ? 'Ativo' : 'Inativo'),
      opcoes: [
        { valor: 'Ativo', rotulo: 'Ativo', tom: 'success' },
        { valor: 'Inativo', rotulo: 'Inativo', tom: 'neutral' }
      ]
    }
  ];

  readonly acoes: AcaoLinha<CupomDto>[] = [
    { rotulo: 'Editar', icone: 'lucidePencil', executar: c => this.editar(c.id) },
    { rotulo: 'Excluir', icone: 'lucideTrash2', destrutiva: true, executar: c => this.excluir(c.id) }
  ];

  readonly tituloLinha = (c: CupomDto) => c.codigo;

  formatarValor(cupom: CupomDto): string {
    if (cupom.tipoDesconto === 'Percentual') {
      return `${cupom.valorDesconto}%`;
    }
    return formatarMoeda(cupom.valorDesconto);
  }

  /** "2 / 5" com limite; sem limite (limiteUsos nulo) não escreve "null": "3 (sem limite)". */
  formatarUsos(cupom: CupomDto): string {
    const usos = cupom.usosAtuais ?? 0;
    return cupom.limiteUsos == null ? `${usos} (sem limite)` : `${usos} / ${cupom.limiteUsos}`;
  }

  formatarData(dataString?: string): string {
    return dataString ? formatarData(dataString) : '';
  }

  novo(): void {
    void this.router.navigate(['/cupons/new']);
  }

  editar(id?: string): void {
    if (id) void this.router.navigate([`/cupons/${id}/edit`]);
  }
}
