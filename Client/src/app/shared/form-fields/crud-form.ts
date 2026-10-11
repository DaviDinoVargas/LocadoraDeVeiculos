import { DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormGroup } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable } from 'rxjs';
import { mensagensDeErro } from '../../core/api-error';
import { NotificacaoService } from '../../core/notificacao.service';

export interface OpcoesCrudForm<TDto, TForm extends { [K in keyof TForm]: AbstractControl }, TCarga = TDto> {
  form: FormGroup<TForm>;
  obter: (id: string) => Observable<TCarga>;
  criar: (payload: TDto) => Observable<unknown>;
  atualizar: (id: string, payload: TDto) => Observable<unknown>;
  /** Rota da listagem: destino ao salvar e ao cancelar. */
  rotaLista: string;
  /** Nome da entidade, no singular e em minúsculas ("grupo de veículo"), para os avisos. */
  singular: string;
  genero?: 'm' | 'f';
  /** Converte o DTO da API em valores do formulário (padrão: o próprio DTO). */
  aoCarregar?: (dto: TCarga) => void;
  /** Monta o payload a partir do formulário (padrão: `form.getRawValue()`). */
  montarPayload?: () => TDto;
}

/** Foca o primeiro campo inválido (o foco vai para o erro ao enviar, SKILL §17.3). */
export function focarPrimeiroInvalido(): void {
  const alvo = document.querySelector<HTMLElement>('main .ng-invalid[formControlName], main .ng-invalid[formcontrolname]');
  const campo = alvo?.matches('input,select,textarea,button') ? alvo : alvo?.querySelector<HTMLElement>('input,select,textarea,button');
  campo?.focus();
}

/**
 * Cadastro e edição com UM componente (SKILL §17.1): lê o `:id` da rota (modo `edit`) ou não (`create`),
 * carrega o registro com esqueleto, salva (criar/atualizar), foca o primeiro erro, mostra os erros do
 * servidor e marca o formulário como "limpo" ao salvar (para a proteção contra perda de alterações).
 * Chamar no construtor (contexto de injeção).
 */
export function crudForm<TDto, TForm extends { [K in keyof TForm]: AbstractControl }, TCarga = TDto>(
  opcoes: OpcoesCrudForm<TDto, TForm, TCarga>
) {
  const route = inject(ActivatedRoute);
  const router = inject(Router);
  const notificacao = inject(NotificacaoService);
  const feminino = opcoes.genero === 'f';

  const id = signal<string | null>(null);
  const carregando = signal(false);
  const salvando = signal(false);
  const erros = signal<string[]>([]);
  const editMode = computed(() => id() !== null);

  route.paramMap.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe(params => {
    const atual = params.get('id');
    id.set(atual);
    if (!atual) return;

    carregando.set(true);
    opcoes.obter(atual).subscribe({
      next: dto => {
        if (opcoes.aoCarregar) {
          opcoes.aoCarregar(dto);
        } else {
          opcoes.form.patchValue(dto as never); // sem conversão própria, o DTO da API é aplicado direto
        }
        opcoes.form.markAsPristine();
        carregando.set(false);
      },
      error: () => {
        // o interceptor já avisou o motivo; volta para a lista
        carregando.set(false);
        void router.navigate([opcoes.rotaLista]);
      }
    });
  });

  function salvar(): void {
    erros.set([]);
    if (opcoes.form.invalid) {
      opcoes.form.markAllAsTouched();
      notificacao.aviso('Verifique os campos destacados.');
      queueMicrotask(focarPrimeiroInvalido);
      return;
    }

    const payload = opcoes.montarPayload ? opcoes.montarPayload() : (opcoes.form.getRawValue() as unknown as TDto);
    const nome = opcoes.singular.charAt(0).toUpperCase() + opcoes.singular.slice(1);
    const atual = id();
    salvando.set(true);

    const chamada = atual ? opcoes.atualizar(atual, payload) : opcoes.criar(payload);
    chamada.subscribe({
      next: () => {
        salvando.set(false);
        opcoes.form.markAsPristine();
        notificacao.sucesso(`${nome} ${atual ? (feminino ? 'atualizada' : 'atualizado') : feminino ? 'criada' : 'criado'}`);
        void router.navigate([opcoes.rotaLista]);
      },
      error: err => {
        salvando.set(false);
        erros.set(mensagensDeErro(err));
      }
    });
  }

  function cancelar(): void {
    void router.navigate([opcoes.rotaLista]);
  }

  return { id, editMode, carregando, salvando, erros, salvar, cancelar };
}
