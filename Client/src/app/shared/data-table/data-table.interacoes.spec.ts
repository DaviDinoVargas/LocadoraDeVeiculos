import { ChangeDetectionStrategy, Component } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, flush, tick } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { DataTableComponent } from './data-table.component';
import { ColunaTabela } from './data-table.types';

interface Veiculo {
  id: string;
  placa: string;
  modelo: string;
  combustivel: string;
  ano: number;
  diaria: number;
}

const FROTA: Veiculo[] = [
  { id: '1', placa: 'AAA1A11', modelo: 'Uno', combustivel: 'Flex', ano: 2020, diaria: 120 },
  { id: '2', placa: 'BBB2B22', modelo: 'Hilux', combustivel: 'Diesel', ano: 2023, diaria: 380 },
  { id: '3', placa: 'CCC3C33', modelo: 'Ônix', combustivel: 'Flex', ano: 2022, diaria: 150 }
];

@Component({
  imports: [DataTableComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-data-table tableId="teste-interacoes" itens="veículos" item="veículo" [colunas]="colunas" [dados]="dados" />`
})
class HostComponent {
  dados: Veiculo[] = FROTA;
  readonly colunas: ColunaTabela<Veiculo>[] = [
    { id: 'placa', cabecalho: 'Placa' },
    { id: 'modelo', cabecalho: 'Modelo' },
    { id: 'combustivel', cabecalho: 'Combustível', tipo: 'enum', opcoes: [{ valor: 'Flex', rotulo: 'Flex' }, { valor: 'Diesel', rotulo: 'Diesel' }] },
    { id: 'ano', cabecalho: 'Ano' },
    { id: 'diaria', cabecalho: 'Diária', tipo: 'moeda' }
  ];
}

/** O que a pessoa faz na tabela (digitar, apertar atalhos, exportar, mudar preferências) e o que ela passa a ver ou baixar. */
describe('DataTableComponent — interações da pessoa', () => {
  const CHAVE = 'tabela:teste-interacoes';
  let fixture: ComponentFixture<HostComponent>;
  let router: Router;

  interface Internos {
    alternarColuna(id: string): void;
    restaurarColunas(): void;
    definirDensidade(densidade: string): void;
    definirTamanho(valor: string): void;
    limparFiltros(): void;
  }
  const tabela = () => fixture.debugElement.children[0].componentInstance as unknown as Internos;
  const el = () => fixture.nativeElement as HTMLElement;
  const campoBusca = () => el().querySelector('input[type="search"]') as HTMLInputElement;
  const placas = () => Array.from(el().querySelectorAll('tbody tr')).map(l => l.querySelector('td')?.textContent?.trim());
  const cabecalhos = () => Array.from(el().querySelectorAll('thead th')).map(th => th.textContent?.trim());
  const consulta = () => TestBed.inject(ActivatedRoute).snapshot.queryParamMap;
  const preferencias = () => JSON.parse(localStorage.getItem(CHAVE) ?? '{}') as { tamanho?: number; densidade?: string; ocultas?: string[] };
  const assentar = async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };
  const irPara = async (queryParams: Record<string, string | null>) => {
    await router.navigate([], { queryParams, queryParamsHandling: 'merge' });
    await assentar();
  };

  beforeEach(async () => {
    localStorage.removeItem(CHAVE);
    TestBed.configureTestingModule({ imports: [HostComponent], providers: [provideRouter([])] });
    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(HostComponent);
    await assentar();
  });

  afterEach(() => localStorage.removeItem(CHAVE));

  describe('busca', () => {
    it('o texto digitado aparece na hora, mas a lista só filtra (e a URL só muda) depois de 300 ms sem digitar', fakeAsync(() => {
      campoBusca().value = 'hil';
      campoBusca().dispatchEvent(new Event('input'));
      fixture.detectChanges();
      expect(campoBusca().value).toBe('hil');

      tick(299);
      expect(consulta().get('q')).withContext('ainda dentro dos 300 ms').toBeNull();

      tick(1);
      flush();
      fixture.detectChanges();
      expect(consulta().get('q')).toBe('hil');
      expect(placas()).toEqual(['BBB2B22']);
    }));

    it('várias teclas seguidas viram uma só busca, com o texto final (não consulta a cada letra)', fakeAsync(() => {
      const navegar = spyOn(router, 'navigate').and.callThrough();
      for (const texto of ['h', 'hi', 'hil']) {
        campoBusca().value = texto;
        campoBusca().dispatchEvent(new Event('input'));
        tick(100);
      }

      tick(300);
      flush();

      expect(navegar).toHaveBeenCalledTimes(1);
      expect(navegar.calls.mostRecent().args[1]?.queryParams).toEqual({ q: 'hil', page: null });
    }));

    it('buscar volta para a primeira página', fakeAsync(() => {
      void router.navigate([], { queryParams: { page: '2' } });
      flush();

      campoBusca().value = 'uno';
      campoBusca().dispatchEvent(new Event('input'));
      tick(300);
      flush();

      expect(consulta().get('page')).toBeNull();
    }));

    it('Esc no campo limpa a busca, a URL e devolve todas as linhas', async () => {
      await irPara({ q: 'uno' });
      expect(placas()).toEqual(['AAA1A11']);

      const evento = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
      campoBusca().dispatchEvent(evento);
      await assentar();

      expect(consulta().get('q')).toBeNull();
      expect(campoBusca().value).toBe('');
      expect(placas().length).toBe(3);
    });

    it('Esc com o campo vazio não faz nada (deixa o Esc valer para fechar diálogos)', () => {
      const evento = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });
      const parar = spyOn(evento, 'stopPropagation').and.callThrough();

      campoBusca().dispatchEvent(evento);

      expect(parar).not.toHaveBeenCalled();
    });

    it('o botão "Limpar busca" limpa o campo e a URL e devolve o foco ao campo', async () => {
      await irPara({ q: 'uno' });

      (el().querySelector('[aria-label="Limpar busca"]') as HTMLButtonElement).click();
      await assentar();

      expect(consulta().get('q')).toBeNull();
      expect(placas().length).toBe(3);
      expect(document.activeElement).toBe(campoBusca());
    });
  });

  describe('atalho "/"', () => {
    const apertarBarra = (alvo: EventTarget, extra: KeyboardEventInit = {}) => {
      const evento = new KeyboardEvent('keydown', { key: '/', bubbles: true, cancelable: true, ...extra });
      alvo.dispatchEvent(evento);
      return evento;
    };

    it('leva o foco para a busca e não digita a barra em lugar nenhum', () => {
      const evento = apertarBarra(document.body);

      expect(document.activeElement).toBe(campoBusca());
      expect(evento.defaultPrevented).toBeTrue();
    });

    it('não rouba a barra de quem está digitando em outro campo', () => {
      const outro = document.createElement('input');
      document.body.appendChild(outro);
      outro.focus();

      const evento = apertarBarra(outro);

      expect(document.activeElement).toBe(outro);
      expect(evento.defaultPrevented).toBeFalse();
      outro.remove();
    });

    it('Ctrl+/ e Cmd+/ são atalhos do navegador/outras telas, não da busca', () => {
      expect(apertarBarra(document.body, { ctrlKey: true }).defaultPrevented).toBeFalse();
      expect(apertarBarra(document.body, { metaKey: true }).defaultPrevented).toBeFalse();
      expect(document.activeElement).not.toBe(campoBusca());
    });
  });

  describe('exportar CSV', () => {
    let conteudo: Promise<string>;
    let arquivo: string;

    beforeEach(() => {
      arquivo = '';
      conteudo = new Promise<string>(resolver => {
        spyOn(URL, 'createObjectURL').and.callFake((blob: Blob | MediaSource) => {
          // Blob.text() descarta o BOM; decodificar os bytes mantendo-o permite conferir o que o Excel vai receber
          void (blob as Blob).arrayBuffer().then(bytes => resolver(new TextDecoder('utf-8', { ignoreBOM: true }).decode(bytes)));
          return 'blob:teste';
        });
      });
      spyOn(URL, 'revokeObjectURL');
      spyOn(HTMLAnchorElement.prototype, 'click').and.callFake(function (this: HTMLAnchorElement) {
        arquivo = this.download;
      });
    });

    const exportar = async () => {
      (el().querySelector('[aria-label="Exportar CSV"]') as HTMLButtonElement).click();
      return (await conteudo).replace('﻿', '').split('\r\n');
    };

    it('baixa o que está na tela: só as colunas visíveis e só as linhas do filtro, com os valores formatados', async () => {
      tabela().alternarColuna('ano');
      await irPara({ combustivel: 'Flex' });

      const [cabecalho, ...linhas] = await exportar();

      expect(cabecalho).toBe('Placa;Modelo;Combustível;Diária');
      expect(linhas.length).toBe(2);
      expect(linhas[0].replace(/ /g, ' ')).toBe('AAA1A11;Uno;Flex;R$ 120,00');
      expect(linhas[1]).toContain('CCC3C33;Ônix;Flex');
      expect(arquivo).toMatch(/^veículos-\d{4}-\d{2}-\d{2}\.csv$/);
    });

    it('o arquivo começa com BOM UTF-8, para o Excel não estragar os acentos', async () => {
      el().querySelector<HTMLButtonElement>('[aria-label="Exportar CSV"]')?.click();

      expect((await conteudo).charCodeAt(0)).toBe(0xfeff);
    });

    it('sem nenhuma linha depois dos filtros o botão de exportar fica desabilitado', async () => {
      await irPara({ q: 'zzzz' });

      expect((el().querySelector('[aria-label="Exportar CSV"]') as HTMLButtonElement).disabled).toBeTrue();
    });
  });

  describe('preferências da tabela', () => {
    it('trocar as linhas por página lembra a escolha, volta para a página 1 e passa a valer para a lista', async () => {
      await irPara({ page: '2' });

      tabela().definirTamanho('10');
      await assentar();

      expect(preferencias().tamanho).toBe(10);
      expect(consulta().get('page')).toBeNull();
      expect(el().textContent).toContain('1–3 de 3 resultados');
    });

    it('a densidade escolhida muda a altura das linhas e fica salva', async () => {
      const altura = () => el().querySelector('tbody td')?.className ?? '';

      tabela().definirDensidade('compacta');
      await assentar();
      expect(altura()).toContain('py-1.5');

      tabela().definirDensidade('confortavel');
      await assentar();
      expect(altura()).toContain('py-4');
      expect(preferencias().densidade).toBe('confortavel');
    });

    it('"Restaurar padrão" devolve as colunas ocultadas e apaga só isso das preferências', async () => {
      tabela().definirDensidade('compacta');
      tabela().alternarColuna('ano');
      tabela().alternarColuna('diaria');
      await assentar();
      expect(cabecalhos()).not.toContain('Ano');
      expect(cabecalhos()).not.toContain('Diária');

      tabela().restaurarColunas();
      await assentar();

      expect(cabecalhos()).toContain('Ano');
      expect(cabecalhos()).toContain('Diária');
      expect(preferencias().ocultas).toBeUndefined();
      expect(preferencias().densidade).withContext('a densidade não é desfeita').toBe('compacta');
    });

    it('"Limpar filtros" tira busca, filtro de coluna e página da URL de uma vez', async () => {
      await irPara({ q: 'uno', combustivel: 'Flex', page: '1' });
      expect(placas()).toEqual(['AAA1A11']);

      tabela().limparFiltros();
      await assentar();

      expect(consulta().get('q')).toBeNull();
      expect(consulta().get('combustivel')).toBeNull();
      expect(campoBusca().value).toBe('');
      expect(placas().length).toBe(3);
    });
  });
});
