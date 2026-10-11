import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';

/** Título da aba: "Veículos · Locadora de Veículos" (cada rota informa só o seu `title`). */
@Injectable({ providedIn: 'root' })
export class TituloDaPaginaStrategy extends TitleStrategy {
  private readonly titulo = inject(Title);

  override updateTitle(estado: RouterStateSnapshot): void {
    const pagina = this.buildTitle(estado);
    this.titulo.setTitle(pagina ? `${pagina} · Locadora de Veículos` : 'Locadora de Veículos');
  }
}
