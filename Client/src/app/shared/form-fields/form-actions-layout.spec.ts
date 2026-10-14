import { Component, input } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { PageComponent } from '../layout/page.component';
import { FormActionsComponent } from './form-actions.component';

/** Tela de formulário como as reais: o <form> é a raiz do componente roteado e a barra fica dentro do app-page. */
@Component({
  selector: 'app-tela-formulario',
  imports: [PageComponent, FormActionsComponent],
  template: `
    <form>
      <app-page largura="estreita">
        <h1>Novo registro</h1>
        <div [style.height.px]="alturaDoConteudo()" class="conteudo"></div>
        <app-form-actions />
      </app-page>
    </form>
  `
})
class TelaFormularioComponent {
  readonly alturaDoConteudo = input(80);
}

/** Casca mínima com o mesmo <main id="conteudo"> do app-shell (o CSS global depende desse id e da classe de rolagem). */
@Component({
  selector: 'app-casca-teste',
  imports: [TelaFormularioComponent],
  template: `
    <div style="height: 600px" class="flex flex-col overflow-hidden">
      <header style="height: 56px" class="shrink-0">cabeçalho</header>
      <main id="conteudo" class="min-h-0 flex-1 overflow-y-auto px-4 py-6 sm:px-6 lg:px-8">
        <app-tela-formulario [alturaDoConteudo]="altura" />
      </main>
    </div>
  `
})
class CascaTesteComponent {
  altura = 80;
}

describe('Barra de ações do formulário — ancorada no rodapé da área de conteúdo', () => {
  const montar = (altura: number) => {
    TestBed.configureTestingModule({ imports: [CascaTesteComponent] });
    const fixture = TestBed.createComponent(CascaTesteComponent);
    fixture.componentInstance.altura = altura;
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    const raiz = fixture.nativeElement as HTMLElement;
    return {
      fixture,
      main: raiz.querySelector('#conteudo') as HTMLElement,
      barra: raiz.querySelector('app-form-actions') as HTMLElement
    };
  };

  afterEach(() => document.querySelectorAll('app-casca-teste').forEach(e => e.remove()));

  it('formulário curto: a barra desce até o fim da área de conteúdo, em vez de ficar logo após o último campo', () => {
    const { main, barra } = montar(80);

    expect(main.scrollHeight).toBeLessThanOrEqual(main.clientHeight + 1); // sem rolagem
    expect(barra.getBoundingClientRect().bottom).toBeCloseTo(main.getBoundingClientRect().bottom, 0);
  });

  it('formulário longo: a barra acompanha a rolagem e continua rente ao rodapé no topo, no meio e no fim', () => {
    const { main, barra } = montar(1500);
    const fundoDoMain = main.getBoundingClientRect().bottom;

    expect(main.scrollHeight).toBeGreaterThan(main.clientHeight);

    for (const posicao of [0, 400, main.scrollHeight]) {
      main.scrollTop = posicao;
      expect(barra.getBoundingClientRect().bottom).withContext(`rolagem ${posicao}`).toBeCloseTo(fundoDoMain, 0);
    }
  });
});
