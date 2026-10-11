import { Directive, ElementRef, HostListener, inject, input } from '@angular/core';
import { NgControl } from '@angular/forms';
import { somenteDigitos } from '../validators/documentos';

export type TipoMascara = 'cpf' | 'cnpj';

/** 12345678900 -> 123.456.789-00 (aplica progressivamente enquanto o usuário digita). */
export function mascararCpf(valor: string | null | undefined): string {
  const d = somenteDigitos(valor).slice(0, 11);
  return d
    .replace(/^(\d{3})(\d)/, '$1.$2')
    .replace(/^(\d{3})\.(\d{3})(\d)/, '$1.$2.$3')
    .replace(/^(\d{3})\.(\d{3})\.(\d{3})(\d)/, '$1.$2.$3-$4');
}

/** 12345678000199 -> 12.345.678/0001-99 */
export function mascararCnpj(valor: string | null | undefined): string {
  const d = somenteDigitos(valor).slice(0, 14);
  return d
    .replace(/^(\d{2})(\d)/, '$1.$2')
    .replace(/^(\d{2})\.(\d{3})(\d)/, '$1.$2.$3')
    .replace(/^(\d{2})\.(\d{3})\.(\d{3})(\d)/, '$1.$2.$3/$4')
    .replace(/^(\d{2})\.(\d{3})\.(\d{3})\/(\d{4})(\d)/, '$1.$2.$3/$4-$5');
}

export function aplicarMascara(tipo: TipoMascara, valor: string | null | undefined): string {
  return tipo === 'cpf' ? mascararCpf(valor) : mascararCnpj(valor);
}

/**
 * Máscara de digitação para CPF/CNPJ: <input formControlName="cpf" appMascara="cpf">.
 * O valor guardado no formulário é o já mascarado, que é o formato que a Web API exige.
 * Antes só o formulário de pessoa física tinha máscara (código próprio) e os demais aceitavam
 * "12345678900" no front para depois a API recusar por formato.
 */
@Directive({
  selector: 'input[appMascara]',
  standalone: true,
  host: { '[attr.maxlength]': 'tamanhoMaximo()', inputmode: 'numeric' }
})
export class MascaraDirective {
  readonly appMascara = input.required<TipoMascara>();

  private readonly el = inject<ElementRef<HTMLInputElement>>(ElementRef);
  private readonly ngControl = inject(NgControl, { optional: true, self: true });

  tamanhoMaximo(): number {
    return this.appMascara() === 'cpf' ? 14 : 18;
  }

  @HostListener('input')
  aoDigitar(): void {
    const mascarado = aplicarMascara(this.appMascara(), this.el.nativeElement.value);

    if (this.el.nativeElement.value !== mascarado) {
      this.el.nativeElement.value = mascarado;
    }
    // Garante que o formulário guarde o valor mascarado, qualquer que seja a ordem dos listeners do input.
    if (this.ngControl?.control && this.ngControl.control.value !== mascarado) {
      this.ngControl.control.setValue(mascarado);
    }
  }
}
