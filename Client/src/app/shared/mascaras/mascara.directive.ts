import { Directive, ElementRef, HostListener, inject, input } from '@angular/core';
import { NgControl } from '@angular/forms';
import { somenteDigitos } from '../validators/documentos';

export type TipoMascara = 'cpf' | 'cnpj' | 'telefone' | 'placa';

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

/** 11987654321 -> (11) 98765-4321; fixo (10 dígitos) -> (11) 3456-7890 */
export function mascararTelefone(valor: string | null | undefined): string {
  const d = somenteDigitos(valor).slice(0, 11);
  if (d.length <= 2) return d.length ? `(${d}` : '';
  if (d.length <= 6) return d.replace(/^(\d{2})(\d+)/, '($1) $2');
  if (d.length <= 10) return d.replace(/^(\d{2})(\d{4})(\d+)/, '($1) $2-$3');
  return d.replace(/^(\d{2})(\d{5})(\d+)/, '($1) $2-$3');
}

/** abc1d23 -> ABC-1D23 (letras e números, maiúsculas, hífen depois das 3 letras) */
export function mascararPlaca(valor: string | null | undefined): string {
  const t = (valor ?? '').toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 7);
  return t.length > 3 ? `${t.slice(0, 3)}-${t.slice(3)}` : t;
}

export function aplicarMascara(tipo: TipoMascara, valor: string | null | undefined): string {
  switch (tipo) {
    case 'cpf': return mascararCpf(valor);
    case 'cnpj': return mascararCnpj(valor);
    case 'telefone': return mascararTelefone(valor);
    case 'placa': return mascararPlaca(valor);
  }
}

/**
 * Máscara de digitação para CPF, CNPJ, telefone e placa: <input formControlName="cpf" appMascara="cpf">.
 * O valor guardado no formulário é o já mascarado, que é o formato que a Web API exige.
 * Antes só o formulário de pessoa física tinha máscara (código próprio) e os demais aceitavam
 * "12345678900" no front para depois a API recusar por formato.
 */
@Directive({
  selector: 'input[appMascara]',
  standalone: true,
  host: { '[attr.maxlength]': 'tamanhoMaximo()', '[attr.inputmode]': "appMascara() === 'placa' ? 'text' : 'numeric'" }
})
export class MascaraDirective {
  readonly appMascara = input.required<TipoMascara>();

  private readonly el = inject<ElementRef<HTMLInputElement>>(ElementRef);
  private readonly ngControl = inject(NgControl, { optional: true, self: true });

  tamanhoMaximo(): number {
    const tamanhos: Record<TipoMascara, number> = { cpf: 14, cnpj: 18, telefone: 15, placa: 8 };
    return tamanhos[this.appMascara()];
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
