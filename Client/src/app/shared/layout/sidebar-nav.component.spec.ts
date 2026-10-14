import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { GrupoMenu } from '../../core/navegacao';
import { SidebarNavComponent } from './sidebar-nav.component';

const TODOS = ['Admin', 'Empresa', 'Funcionario'];

const GRUPOS: GrupoMenu[] = [
  {
    rotulo: 'Gestão',
    itens: [
      { rotulo: 'Condutores', icone: 'lucideIdCard', caminho: '/condutores', roles: TODOS },
      {
        rotulo: 'Veículos',
        icone: 'lucideCar',
        caminho: '/veiculos',
        roles: TODOS,
        filhos: [
          { rotulo: 'Todos os veículos', icone: 'lucideCar', caminho: '/veiculos', roles: TODOS },
          { rotulo: 'Grupos', icone: 'lucideLayers', caminho: '/grupoveiculos', roles: TODOS },
          { rotulo: 'Planos', icone: 'lucideCoins', caminho: '/planos-cobranca?ativos=1', roles: TODOS }
        ]
      }
    ]
  }
];

describe('SidebarNavComponent — navegação principal', () => {
  let router: Router;
  let app: ApplicationRef;

  const montar = (recolhido: boolean) => {
    TestBed.configureTestingModule({ imports: [SidebarNavComponent], providers: [provideRouter([])] });
    router = TestBed.inject(Router);
    app = TestBed.inject(ApplicationRef);
    spyOn(router, 'navigate').and.resolveTo(true);

    const fixture = TestBed.createComponent(SidebarNavComponent);
    fixture.componentRef.setInput('grupos', GRUPOS);
    fixture.componentRef.setInput('recolhido', recolhido);
    fixture.detectChanges();
    return fixture;
  };

  const overlay = () => document.querySelector('.cdk-overlay-container') as HTMLElement;
  const botaoPorRotulo = (raiz: HTMLElement, rotulo: string) =>
    Array.from(raiz.querySelectorAll('button')).find(b => b.getAttribute('aria-label')?.startsWith(rotulo)) as HTMLButtonElement;

  it('expandida: mostra o grupo, o item com subitens e permite abrir os filhos', () => {
    const fixture = montar(false);
    const raiz = fixture.nativeElement as HTMLElement;

    expect(raiz.textContent).toContain('Gestão');
    const veiculos = Array.from(raiz.querySelectorAll('button')).find(b => b.textContent?.includes('Veículos')) as HTMLButtonElement;
    expect(veiculos.getAttribute('aria-expanded')).toBe('false');

    veiculos.click();
    fixture.detectChanges();

    expect(veiculos.getAttribute('aria-expanded')).toBe('true');
    expect(raiz.textContent).toContain('Grupos');
    expect(raiz.textContent).toContain('Planos');
  });

  it('recolhida (só ícones): item com subitens abre um menu ao lado com os filhos alcançáveis', () => {
    const fixture = montar(true);
    const raiz = fixture.nativeElement as HTMLElement;

    expect(raiz.textContent).not.toContain('Grupos'); // sem rótulos no trilho

    botaoPorRotulo(raiz, 'Veículos').click();
    fixture.detectChanges();
    app.tick();

    const itens = Array.from(overlay().querySelectorAll('button')).map(b => b.textContent?.trim());
    expect(itens).toEqual(['Todos os veículos', 'Grupos', 'Planos']);
  });

  it('recolhida: escolher um subitem navega para a rota e preserva o filtro da URL', () => {
    const fixture = montar(true);
    botaoPorRotulo(fixture.nativeElement as HTMLElement, 'Veículos').click();
    fixture.detectChanges();
    app.tick();

    const planos = Array.from(overlay().querySelectorAll('button')).find(b => b.textContent?.trim() === 'Planos') as HTMLButtonElement;
    planos.click();

    expect(router.navigate).toHaveBeenCalledWith(['/planos-cobranca'], { queryParams: { ativos: '1' } });
  });

  it('recolhida: item sem subitens continua sendo um link direto com nome acessível', () => {
    const fixture = montar(true);
    const link = (fixture.nativeElement as HTMLElement).querySelector('a[aria-label="Condutores"]');

    expect(link).not.toBeNull();
  });
});
