import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { CelulaDirective, DataTableComponent } from './data-table.component';
import { AcaoLinha, ColunaTabela } from './data-table.types';

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
  imports: [DataTableComponent, CelulaDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-data-table
      tableId="teste-frota"
      itens="veículos"
      item="veículo"
      [colunas]="colunas"
      [dados]="dados()"
      [carregando]="carregando()"
      [erro]="erro()"
      [acoesLinha]="acoes"
      [tituloLinha]="titulo"
      tituloVazio="Nenhum veículo"
      (recarregar)="recarregou = recarregou + 1"
    >
      <ng-template appCelula="modelo" let-v><b class="modelo-custom">{{ v.modelo }}</b></ng-template>
    </app-data-table>
  `
})
class HostComponent {
  recarregou = 0;
  readonly dados = signal<Veiculo[]>(FROTA);
  readonly carregando = signal(false);
  readonly erro = signal(false);
  readonly executou: string[] = [];
  readonly titulo = (v: Veiculo) => v.modelo;
  readonly colunas: ColunaTabela<Veiculo>[] = [
    { id: 'placa', cabecalho: 'Placa' },
    { id: 'modelo', cabecalho: 'Modelo' },
    { id: 'combustivel', cabecalho: 'Combustível', tipo: 'enum', opcoes: [{ valor: 'Flex', rotulo: 'Flex', tom: 'success' }, { valor: 'Diesel', rotulo: 'Diesel', tom: 'warning' }] },
    { id: 'ano', cabecalho: 'Ano' },
    { id: 'diaria', cabecalho: 'Diária', tipo: 'moeda' }
  ];
  readonly acoes: AcaoLinha<Veiculo>[] = [{ rotulo: 'Excluir', icone: 'lucideTrash2', destrutiva: true, executar: v => this.executou.push(v.id) }];
}

describe('DataTableComponent — tabela compartilhada', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;
  let router: Router;

  const el = () => fixture.nativeElement as HTMLElement;
  const linhas = () => Array.from(el().querySelectorAll('tbody tr'));
  const celulas = (linha: Element) => Array.from(linha.querySelectorAll('td')).map(td => td.textContent?.trim().replace(/ /g, ' '));
  const placas = () => linhas().map(l => celulas(l)[0]);
  const parametros = () => TestBed.inject(ActivatedRoute).snapshot.queryParamMap;

  /** Navega pela URL (o estado da tabela mora nos query params) e espera a tela assentar. */
  const irPara = async (queryParams: Record<string, string | null>) => {
    await router.navigate([], { queryParams, queryParamsHandling: 'merge' });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    localStorage.removeItem('tabela:teste-frota');
    TestBed.configureTestingModule({ imports: [HostComponent], providers: [provideRouter([])] });
    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  afterEach(() => localStorage.removeItem('tabela:teste-frota'));

  it('mostra uma linha por registro, com moeda pt-BR e selo (ícone + texto) para o enum', () => {
    expect(linhas().length).toBe(3);
    expect(celulas(linhas()[0])).toContain('R$ 120,00');
    expect(linhas()[0].querySelector('app-status-badge')?.textContent).toContain('Flex');
    expect(linhas()[0].querySelector('app-status-badge ng-icon')).not.toBeNull();
  });

  it('usa o template de célula customizada quando a coluna tem um', () => {
    expect(linhas()[0].querySelector('.modelo-custom')?.textContent).toBe('Uno');
  });

  it('o contador diz quantos resultados há', () => {
    expect(el().textContent).toContain('1–3 de 3 resultados');
  });

  it('a busca ignora acento e caixa e vai para a URL (compartilhável)', async () => {
    await irPara({ q: 'onix' });

    expect(placas()).toEqual(['CCC3C33']);
    expect(el().textContent).toContain('filtrado de 3');
    expect(el().textContent).toContain('Busca: “onix”');
  });

  it('filtro por coluna (enum) mostra só os marcados e aparece como chip removível', async () => {
    await irPara({ combustivel: 'Diesel' });

    expect(placas()).toEqual(['BBB2B22']);
    expect(el().textContent).toContain('Combustível: Diesel');
    (el().querySelector('[aria-label="Remover filtro Combustível: Diesel"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(parametros().get('combustivel')).toBeNull();
  });

  it('sem resultado para os filtros é diferente de "nada cadastrado" e oferece limpar', async () => {
    await irPara({ q: 'zzzz' });

    expect(el().textContent).toContain('Nenhum resultado para os filtros aplicados');
    expect(el().textContent).toContain('Limpar filtros');
    expect(el().textContent).not.toContain('Nenhum veículo');
  });

  it('sem dados mostra o estado vazio com o texto da tela', () => {
    host.dados.set([]);
    fixture.detectChanges();

    expect(el().textContent).toContain('Nenhum veículo');
    expect(el().querySelector('table')).toBeNull();
  });

  it('erro mostra a mensagem com "Tentar novamente" e emite recarregar', () => {
    host.erro.set(true);
    fixture.detectChanges();

    const botao = Array.from(el().querySelectorAll('button')).find(b => b.textContent?.includes('Tentar novamente'))!;
    botao.click();

    expect(host.recarregou).toBe(1);
  });

  it('carregando sem dados mostra esqueleto (aria-busy) em vez de tabela vazia', () => {
    host.dados.set([]);
    host.carregando.set(true);
    fixture.detectChanges();

    expect(el().querySelector('[aria-busy="true"]')).not.toBeNull();
    expect(linhas().length).toBe(6);
    expect(el().querySelectorAll('hlm-skeleton').length).toBeGreaterThan(0);
  });

  it('ordenação: clique no cabeçalho alterna crescente → decrescente → nenhuma, com aria-sort', async () => {
    const cabecalho = () => Array.from(el().querySelectorAll('thead th')).find(th => th.textContent?.includes('Diária')) as HTMLElement;
    const clicar = async () => {
      (cabecalho().querySelector('button') as HTMLButtonElement).click();
      await fixture.whenStable();
      fixture.detectChanges();
    };

    await clicar();
    expect(placas()).toEqual(['AAA1A11', 'CCC3C33', 'BBB2B22']);
    expect(cabecalho().getAttribute('aria-sort')).toBe('ascending');

    await clicar();
    expect(placas()).toEqual(['BBB2B22', 'CCC3C33', 'AAA1A11']);
    expect(cabecalho().getAttribute('aria-sort')).toBe('descending');

    await clicar();
    expect(placas()).toEqual(['AAA1A11', 'BBB2B22', 'CCC3C33']);
    expect(cabecalho().getAttribute('aria-sort')).toBe('none');
  });

  it('paginação: tamanho da página pela URL e navegação entre páginas', async () => {
    host.dados.set(Array.from({ length: 30 }, (_, i) => ({ ...FROTA[0], id: `x${i}`, placa: `P${String(i).padStart(2, '0')}` })));
    fixture.detectChanges();
    expect(linhas().length).toBe(25);
    expect(el().textContent).toContain('Página 1 de 2');

    await irPara({ page: '2' });
    expect(linhas().length).toBe(5);
    expect(el().textContent).toContain('26–30 de 30 resultados');

    await irPara({ page: null, size: '10' });
    expect(linhas().length).toBe(10);
    expect(el().textContent).toContain('Página 1 de 3');
  });

  it('página pedida além do fim volta para a última (ex.: depois de excluir)', async () => {
    await irPara({ page: '9' });

    expect(linhas().length).toBe(3);
    expect(el().textContent).toContain('Página 1 de 1');
  });

  it('a ação da linha fica num menu com rótulo acessível que cita o registro', () => {
    const botoes = Array.from(el().querySelectorAll('tbody button[aria-label^="Ações para"]'));

    expect(botoes.length).toBe(3);
    expect(botoes[0].getAttribute('aria-label')).toBe('Ações para Uno');
  });

  it('ocultar uma coluna pelo menu a tira da tabela e lembra a escolha por tableId', async () => {
    const tabela = fixture.debugElement.children[0].componentInstance as { alternarColuna(id: string): void };
    tabela.alternarColuna('ano');
    fixture.detectChanges();

    expect(Array.from(el().querySelectorAll('thead th')).some(th => th.textContent?.includes('Ano'))).toBeFalse();
    expect(localStorage.getItem('tabela:teste-frota')).toContain('ano');
  });

  it('a tabela é semântica: caption oculta e cabeçalhos com scope', () => {
    expect(el().querySelector('table caption.sr-only')?.textContent).toContain('Lista de veículos');
    expect(el().querySelector('thead th')?.getAttribute('scope')).toBe('col');
  });

  it('no mobile há uma lista de cartões alternativa (a tabela some por CSS, sem rolagem horizontal)', () => {
    const cartoes = el().querySelectorAll('ul.md\\:hidden > li');

    expect(cartoes.length).toBe(3);
    expect(cartoes[0].textContent).toContain('AAA1A11');
  });
});
