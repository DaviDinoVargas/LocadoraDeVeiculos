import { computed, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { filter } from 'rxjs/operators';
import { ConfirmacaoService } from '../../core/confirmacao.service';
import { NotificacaoService } from '../../core/notificacao.service';

export type EstadoLista = 'carregando' | 'pronto' | 'erro';

export interface OpcoesCrudLista<T> {
  listar: () => Observable<T[]>;
  excluir: (id: string) => Observable<unknown>;
  /** Nome do que a lista mostra, no singular e em minúsculas ("grupo de veículo"): textos do diálogo e do aviso. */
  singular: string;
  genero?: 'm' | 'f';
  /** Carrega ao criar (padrão). Telas que precisam de algo antes (ex.: o papel do usuário) passam `false`. */
  carregarAoCriar?: boolean;
}

/**
 * Carregamento e exclusão padrão das listagens, para ser chamado no construtor (contexto de injeção):
 * estado carregando/pronto/erro, recarregar, e exclusão com diálogo de confirmação destrutivo (cancelar
 * NÃO exclui). Erros do servidor já viram aviso no interceptor global; aqui só se muda o estado da tela
 * (que mostra "Tentar novamente").
 */
export function crudLista<T extends { id?: string }>(opcoes: OpcoesCrudLista<T>) {
  const confirmacao = inject(ConfirmacaoService);
  const notificacao = inject(NotificacaoService);
  const feminino = opcoes.genero === 'f';

  const dados = signal<T[]>([]);
  const estado = signal<EstadoLista>('carregando');

  function carregar(): void {
    estado.set('carregando');
    opcoes.listar().subscribe({
      next: lista => {
        dados.set(lista ?? []);
        estado.set('pronto');
      },
      error: () => estado.set('erro')
    });
  }

  function excluir(id?: string): void {
    if (!id) return;

    confirmacao
      .confirmar({
        titulo: 'Confirmar exclusão',
        mensagem: `Deseja realmente excluir ${feminino ? 'esta' : 'este'} ${opcoes.singular}?`,
        confirmar: 'Excluir',
        destrutivo: true
      })
      .pipe(filter(Boolean))
      .subscribe(() => {
        opcoes.excluir(id).subscribe({
          next: () => {
            const nome = opcoes.singular.charAt(0).toUpperCase() + opcoes.singular.slice(1);
            notificacao.sucesso(`${nome} excluíd${feminino ? 'a' : 'o'}`);
            carregar();
          }
          // erro: o interceptor global já mostrou o motivo real do servidor (ex.: "existem aluguéis vinculados")
        });
      });
  }

  if (opcoes.carregarAoCriar !== false) carregar();

  return {
    dados,
    estado,
    carregando: computed(() => estado() === 'carregando'),
    erro: computed(() => estado() === 'erro'),
    carregar,
    excluir
  };
}
