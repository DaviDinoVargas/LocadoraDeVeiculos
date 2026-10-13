import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DataTableComponent } from '../../shared/data-table/data-table.component';
import { ColunaTabela } from '../../shared/data-table/data-table.types';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { RegistroAuditoria } from './auditoria.model';
import { AuditoriaService } from './auditoria.service';

const ROTULOS_DE_ACAO: Record<string, string> = {
  Criado: 'Criado',
  Alterado: 'Alterado',
  'Excluído': 'Excluído',
  Removido: 'Removido',
  LoginOk: 'Login',
  LoginFalhou: 'Falha de login',
  LoginBloqueado: 'Login bloqueado',
  LoginFacialOk: 'Login facial',
  LoginFacialFalhou: 'Falha de login facial',
  LoginFacialBloqueado: 'Login facial bloqueado',
  SenhaAlterada: 'Senha alterada',
  SenhaRedefinida: 'Senha redefinida',
  DoisFatoresAtivado: '2FA ativado',
  DoisFatoresDesativado: '2FA desativado',
  ContaEncerrada: 'Conta encerrada'
};

/**
 * Trilha de auditoria da empresa (S14), só para o papel Empresa. Mostra quem fez o quê, quando e de onde; por desenho
 * os registros não têm os valores alterados (só os nomes dos campos), nem dados pessoais.
 */
@Component({
  selector: 'app-auditoria-list',
  imports: [PageComponent, PageHeaderComponent, DataTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page>
      <app-page-header
        titulo="Auditoria"
        descricao="Quem fez o quê, quando e de onde. Os registros guardam só os nomes dos campos alterados, nunca os valores. O IP é anonimizado depois de 180 dias."
      />

      <app-data-table
        tableId="auditoria"
        itens="registros de auditoria"
        item="registro"
        [colunas]="colunas"
        [dados]="registros()"
        [carregando]="carregando()"
        [erro]="erro()"
        [tituloLinha]="tituloLinha"
        tituloVazio="Nenhum registro ainda"
        descricaoVazio="As ações da sua empresa aparecem aqui conforme acontecem."
        (recarregar)="carregar()"
      />
    </app-page>
  `
})
export class AuditoriaListComponent implements OnInit {
  private readonly svc = inject(AuditoriaService);

  readonly registros = signal<RegistroAuditoria[]>([]);
  readonly carregando = signal(true);
  readonly erro = signal(false);

  readonly colunas: ColunaTabela<RegistroAuditoria>[] = [
    {
      id: 'emUtc',
      cabecalho: 'Quando',
      valor: r => r.emUtc,
      formatar: r => new Date(r.emUtc.endsWith('Z') ? r.emUtc : `${r.emUtc}Z`).toLocaleString('pt-BR')
    },
    { id: 'acao', cabecalho: 'Ação', filtravel: true, formatar: r => ROTULOS_DE_ACAO[r.acao] ?? r.acao },
    { id: 'entidade', cabecalho: 'Entidade', filtravel: true },
    { id: 'camposAlterados', cabecalho: 'Campos alterados', truncar: 40, prioridade: 'baixa' },
    { id: 'usuarioId', cabecalho: 'Usuário', truncar: 12, prioridade: 'baixa' },
    { id: 'enderecoIp', cabecalho: 'IP', prioridade: 'baixa' }
  ];

  readonly tituloLinha = (r: RegistroAuditoria) => `${ROTULOS_DE_ACAO[r.acao] ?? r.acao} · ${r.entidade}`;

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    this.carregando.set(true);
    this.erro.set(false);
    this.svc.listar().subscribe({
      next: lista => {
        this.registros.set(lista ?? []);
        this.carregando.set(false);
      },
      error: () => {
        this.erro.set(true);
        this.carregando.set(false);
      }
    });
  }
}
