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
import { mascararCpf } from '../../shared/mascaras/mascara.directive';
import { FuncionarioDto } from './funcionario.model';
import { FuncionariosService } from './funcionarios.service';

@Component({
  selector: 'app-funcionarios-list',
  imports: [RouterLink, NgIcon, HlmButtonImports, PageComponent, PageHeaderComponent, DataTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './funcionarios-list.component.html'
})
export class FuncionariosListComponent {
  private readonly svc = inject(FuncionariosService);
  private readonly router = inject(Router);

  private readonly lista = crudLista<FuncionarioDto>({
    listar: () => this.svc.listar(),
    excluir: id => this.svc.excluir(id),
    singular: 'funcionário'
  });
  readonly funcionarios = this.lista.dados;
  readonly carregando = this.lista.carregando;
  readonly erro = this.lista.erro;
  readonly carregar = this.lista.carregar;
  readonly excluir = this.lista.excluir;

  readonly colunas: ColunaTabela<FuncionarioDto>[] = [
    { id: 'nomeCompleto', cabecalho: 'Nome' },
    { id: 'cpf', cabecalho: 'CPF', formatar: f => mascararCpf(f.cpf) },
    { id: 'email', cabecalho: 'E-mail', truncar: 32, prioridade: 'baixa' },
    { id: 'salario', cabecalho: 'Salário', tipo: 'moeda' },
    { id: 'admissaoEmUtc', cabecalho: 'Admissão', tipo: 'data' }
  ];

  readonly acoes: AcaoLinha<FuncionarioDto>[] = [
    { rotulo: 'Editar', icone: 'lucidePencil', executar: f => this.editar(f.id) },
    { rotulo: 'Excluir', icone: 'lucideTrash2', destrutiva: true, executar: f => this.excluir(f.id) }
  ];

  readonly tituloLinha = (f: FuncionarioDto) => f.nomeCompleto;

  formatarCPF(cpf: string): string {
    return mascararCpf(cpf);
  }

  formatarData(dataString: string): string {
    return formatarData(dataString);
  }

  formatarSalario(salario: number): string {
    return formatarMoeda(salario);
  }

  novo(): void {
    void this.router.navigate(['/funcionarios/new']);
  }

  editar(id?: string): void {
    if (id) void this.router.navigate([`/funcionarios/${id}/edit`]);
  }
}
