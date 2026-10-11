import { ChangeDetectionStrategy, Component, OnDestroy, booleanAttribute, computed, effect, inject, input } from '@angular/core';
import { ValidationErrors } from '@angular/forms';
import { NgIcon } from '@ng-icons/core';
import { BrnField, BrnFieldA11yService } from '@spartan-ng/brain/field';
import { HlmField, HlmFieldImports } from '@spartan-ng/helm/field';

const MENSAGENS_PADRAO: Record<string, (valor: unknown) => string> = {
  required: () => 'Campo obrigatório',
  email: () => 'Informe um e-mail válido',
  minlength: v => `Use ao menos ${(v as { requiredLength: number }).requiredLength} caracteres`,
  maxlength: v => `Use no máximo ${(v as { requiredLength: number }).requiredLength} caracteres`,
  min: v => `O valor mínimo é ${(v as { min: number }).min}`,
  max: v => `O valor máximo é ${(v as { max: number }).max}`,
  pattern: () => 'Formato inválido',
  cpfInvalido: () => 'CPF inválido',
  cnpjInvalido: () => 'CNPJ inválido'
};

/** Primeira mensagem de erro de um controle (erros do servidor chegam como `{ servidor: 'texto' }`). */
export function mensagemDeErro(errors: ValidationErrors | null, personalizadas: Record<string, string> = {}): string {
  if (!errors) return '';
  const chave = Object.keys(errors)[0];
  if (!chave) return '';
  if (personalizadas[chave]) return personalizadas[chave];
  if (typeof errors[chave] === 'string') return errors[chave] as string;
  return MENSAGENS_PADRAO[chave]?.(errors[chave]) ?? 'Valor inválido';
}

let proximoId = 0;

/**
 * Wrapper único de campo (SKILL §17.1): rótulo SEMPRE visível acima, ajuda abaixo, obrigatoriedade
 * em texto e erro específico com ícone. O próprio campo É o `hlm-field` (host directive): o controle projetado
 * (input/select) fica sob este elemento na árvore de injeção, e só assim o Spartan liga rótulo ↔ campo
 * (`for`/`id`) e o campo fica `aria-invalid`. O erro só aparece depois do blur (touched) ou do envio (o
 * formulário chama `markAllAsTouched()`), e é registrado no `aria-describedby` do controle.
 *
 *   <app-form-field label="E-mail" required hint="Usado para entrar">
 *     <input hlmInput type="email" formControlName="email" autocomplete="email" />
 *   </app-form-field>
 */
@Component({
  selector: 'app-form-field',
  imports: [HlmFieldImports, NgIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  hostDirectives: [HlmField],
  host: { class: 'min-w-0 gap-1.5' },
  template: `
    @if (rotulo()) {
      <label hlmFieldLabel class="text-label-l">
        {{ rotulo() }}
        @if (obrigatorio()) {
          <span class="text-body-s text-muted-foreground font-normal">(obrigatório)</span>
        }
      </label>
    }
    <ng-content />
    @if (ajuda()) {
      <p hlmFieldDescription class="text-body-s" [id]="idAjuda">{{ ajuda() }}</p>
    }
    @if (erroVisivel()) {
      <p [id]="idErro" role="alert" class="text-body-s text-destructive flex items-start gap-1">
        <ng-icon name="lucideCircleAlert" class="mt-0.5 shrink-0" aria-hidden="true" />
        <span>{{ mensagem() }}</span>
      </p>
    }
  `
})
export class FormFieldComponent implements OnDestroy {
  readonly rotulo = input('', { alias: 'label' });
  readonly ajuda = input('', { alias: 'hint' });
  readonly obrigatorio = input(false, { alias: 'required', transform: booleanAttribute });
  /** Mensagens por chave de erro para este campo (sobrepõem as padrão). */
  readonly mensagens = input<Record<string, string>>({});

  private readonly campo = inject(BrnField);
  private readonly a11y = inject(BrnFieldA11yService);

  private readonly numero = proximoId++;
  protected readonly idErro = `app-campo-erro-${this.numero}`;
  protected readonly idAjuda = `app-campo-ajuda-${this.numero}`;

  protected readonly mensagem = computed(() => mensagemDeErro(this.campo.errors(), this.mensagens()));
  protected readonly erroVisivel = computed(() => !!this.campo.controlState()?.spartanInvalid && !!this.mensagem());

  constructor() {
    effect(() => {
      if (this.erroVisivel()) this.a11y.registerError(this.idErro);
      else this.a11y.unregisterError(this.idErro);
    });
    effect(() => {
      if (this.ajuda()) this.a11y.registerDescription(this.idAjuda);
      else this.a11y.unregisterDescription(this.idAjuda);
    });
  }

  ngOnDestroy(): void {
    this.a11y.unregisterError(this.idErro);
    this.a11y.unregisterDescription(this.idAjuda);
  }
}
