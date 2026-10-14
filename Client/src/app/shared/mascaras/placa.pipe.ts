import { Pipe, PipeTransform } from '@angular/core';
import { mascararPlaca } from './mascara.directive';

/** Mostra a placa sempre no mesmo formato da lista de veículos: abc1d23 -> ABC-1D23. */
@Pipe({ name: 'placa' })
export class PlacaPipe implements PipeTransform {
  transform(valor: string | null | undefined): string {
    return mascararPlaca(valor);
  }
}
