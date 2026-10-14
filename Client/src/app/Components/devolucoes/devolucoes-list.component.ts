import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { crudLista } from '../../shared/data-table/crud-lista';
import { DataTableComponent } from '../../shared/data-table/data-table.component';
import { AcaoLinha, ColunaTabela } from '../../shared/data-table/data-table.types';
import { formatarData, formatarMoeda } from '../../shared/data-table/data-table.util';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { PdfService } from '../pdf/pdf.service';
import { SelecionarDevolucoesDto } from './devolucao.model';
import { DevolucoesService } from './devolucoes.service';
import { mascararPlaca } from '../../shared/mascaras/mascara.directive';

@Component({
  selector: 'app-devolucoes-list',
  imports: [NgIcon, HlmButtonImports, PageComponent, PageHeaderComponent, DataTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './devolucoes-list.component.html'
})
export class DevolucoesListComponent {
  private readonly svc = inject(DevolucoesService);
  private readonly pdf = inject(PdfService);
  private readonly router = inject(Router);

  private readonly lista = crudLista<SelecionarDevolucoesDto>({
    listar: () => this.svc.listar(),
    excluir: id => this.svc.excluir(id),
    singular: 'devolução',
    genero: 'f'
  });
  readonly devolucoes = this.lista.dados;
  readonly carregando = this.lista.carregando;
  readonly erro = this.lista.erro;
  readonly carregar = this.lista.carregar;
  readonly excluir = this.lista.excluir;

  readonly colunas: ColunaTabela<SelecionarDevolucoesDto>[] = [
    { id: 'condutorNome', cabecalho: 'Condutor' },
    { id: 'automovelPlaca', cabecalho: 'Veículo', formatar: d => mascararPlaca(d.automovelPlaca) },
    { id: 'clienteNome', cabecalho: 'Cliente' },
    { id: 'dataDevolucao', cabecalho: 'Data da devolução', tipo: 'data' },
    { id: 'quilometragemFinal', cabecalho: 'Km final', tipo: 'numero', formatar: d => `${d.quilometragemFinal} km` },
    { id: 'valorTotal', cabecalho: 'Valor total', tipo: 'moeda' }
  ];

  readonly acoes: AcaoLinha<SelecionarDevolucoesDto>[] = [
    { rotulo: 'Visualizar', icone: 'lucideEye', executar: d => this.visualizar(d.id) },
    { rotulo: 'Excluir', icone: 'lucideTrash2', destrutiva: true, executar: d => this.excluir(d.id) }
  ];

  readonly tituloLinha = (d: SelecionarDevolucoesDto) => `${d.clienteNome} — ${mascararPlaca(d.automovelPlaca)}`;

  async exportarPdf(): Promise<void> {
    if (this.devolucoes().length === 0) return;
    await this.pdf.gerarRelatorioDevolucoes(this.devolucoes());
  }

  formatarData(dataString: string): string {
    return formatarData(dataString);
  }

  formatarMoeda(valor: number): string {
    return formatarMoeda(valor);
  }

  visualizar(id?: string): void {
    if (id) void this.router.navigate([`/devolucoes/${id}`]);
  }
}
