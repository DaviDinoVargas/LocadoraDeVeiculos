import { DOCUMENT } from '@angular/common';
import { Injectable, effect, inject, signal } from '@angular/core';

export type Tema = 'claro' | 'escuro';

const CHAVE = 'tema';

/**
 * Tema claro/escuro (classe `.dark` no <html>, como o Spartan/Tailwind esperam).
 * Na primeira visita segue o tema do sistema; depois lembra a escolha. O script do index.html já
 * aplica a classe antes da primeira pintura, então aqui só se lê o estado e se reage ao botão.
 */
@Injectable({ providedIn: 'root' })
export class TemaService {
  private readonly doc = inject(DOCUMENT);

  readonly tema = signal<Tema>(this.doc.documentElement.classList.contains('dark') ? 'escuro' : 'claro');

  constructor() {
    effect(() => {
      this.doc.documentElement.classList.toggle('dark', this.tema() === 'escuro');
    });
  }

  alternar(): void {
    const novo: Tema = this.tema() === 'escuro' ? 'claro' : 'escuro';
    // aplica a classe já, antes dos sinais derivados (gráficos leem as cores dos tokens ao reagir ao tema)
    this.doc.documentElement.classList.toggle('dark', novo === 'escuro');
    this.tema.set(novo);
    try {
      localStorage.setItem(CHAVE, novo);
    } catch {
      // armazenamento indisponível (janela privada): o tema vale só nesta visita
    }
  }
}
