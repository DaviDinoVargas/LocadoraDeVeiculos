import { ChangeDetectionStrategy, Component, DestroyRef, booleanAttribute, computed, contentChild, effect, inject, input, signal } from '@angular/core';
import { AbstractControl, NgControl, ValidationErrors } from '@angular/forms';
import { NgIcon } from '@ng-icons/core';
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

/**
 * Wrapper único de campo (SKILL §17.1): rótulo SEMPRE visível acima, ajuda abaixo, obrigatoriedade
 * em texto e erro específico com ícone. A ligação rótulo ↔ campo ↔ erro (`for`, `aria-describedby`,
 * `aria-invalid`) é do Spartan (brain/field). O erro só aparece depois do blur (touched) ou do envio
 * (o formulário chama `markAllAsTouched()`).
 *
 *   <app-form-field label="E-mail" obrigatorio ajuda="Usado para entrar">
 *     <input hlmInput type="email" formControlName="email" autocomplete="email" />
 *   </app-form-field>
 */
@Component({
  selector: 'app-form-field',
  imports: [HlmFieldImports, NgIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // O próprio campo É o `hlm-field` (host directive): o controle projetado (input/select) fica sob este
  // elemento na árvore de injeção, e só assim o Spartan liga rótulo ↔ campo ↔ erro (for, aria-describedby).
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
        <p hlmFieldDescription class="text-body-s">{{ ajuda() }}</p>
      }
      <hlm-field-error class="text-body-s flex items-start gap-1">
        <ng-icon name="lucideCircleAlert" class="mt-0.5 shrink-0" aria-hidden="true" />
        <span>{{ mensagem() }}</span>
      </hlm-field-error>
  `
})
export class FormFieldComponent {
  readonly rotulo = input('', { alias: 'label' });
  readonly ajuda = input('', { alias: 'hint' });
  readonly obrigatorio = input(false, { alias: 'required', transform: booleanAttribute });
  /** Mensagens por chave de erro para este campo (sobrepõem as padrão). */
  readonly mensagens = input<Record<string, string>>({});
  /** Controle de fora do campo (ex.: erro no grupo, como "senhas diferentes"). Por padrão usa o do campo projetado. */
  readonly controle = input<AbstractControl | null>(null, { alias: 'control' });

  private readonly ngControl = contentChild(NgControl);
  private readonly erros = signal<ValidationErrors | null>(null);

  protected readonly mensagem = computed(() => mensagemDeErro(this.erros(), this.mensagens()));

  constructor() {
    const destroyRef = inject(DestroyRef);
    effect(onCleanup => {
      const controle = this.controle() ?? this.ngControl()?.control ?? null;
      if (!controle) return;
      const atualizar = () => this.erros.set(controle.errors);
      atualizar();
      const sub = controle.events.subscribe(atualizar);
      onCleanup(() => sub.unsubscribe());
    });
    destroyRef.onDestroy(() => this.erros.set(null));
  }
}
