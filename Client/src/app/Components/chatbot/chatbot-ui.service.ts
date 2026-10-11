import { Injectable, signal } from '@angular/core';

/** Estado de abertura do painel do assistente: o botão fica na barra superior e o painel no app-root. */
@Injectable({ providedIn: 'root' })
export class ChatbotUiService {
  readonly aberto = signal(false);

  alternar(): void {
    this.aberto.update(v => !v);
  }

  fechar(): void {
    this.aberto.set(false);
  }
}
