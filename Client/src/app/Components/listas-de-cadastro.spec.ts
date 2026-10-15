import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Observable, of } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { NotificacaoService } from '../core/notificacao.service';
import { criarNotificacaoFalsa } from '../core/testing/notificacao-fake.spec';
import { ClientesService } from './clientes/clientes.service';
import { CondutoresListComponent } from './condutor/condutores-list.component';
import { CondutoresService } from './condutor/condutores.service';
import { CuponsListComponent } from './cupons/cupons-list.component';
import { CuponsService } from './cupons/cupons.service';
import { DesafiosCupomListComponent } from './desafios-cupom/desafios-cupom-list.component';
import { DesafiosCupomService } from './desafios-cupom/desafios-cupom.service';
import { FuncionariosListComponent } from './funcionarios/funcionarios-list.component';
import { FuncionariosService } from './funcionarios/funcionarios.service';
import { GruposVeiculoListComponent } from './grupoveiculos/grupos-veiculo-list.component';
import { GruposVeiculoService } from './grupoveiculos/grupos-veiculo.service';
import { ParceirosListComponent } from './parceiros/parceiros-list.component';
import { ParceirosService } from './parceiros/parceiros.service';
import { PlanosCobrancaListComponent } from './planocobranca/planos-cobranca-list.component';
import { PlanosCobrancaService } from './planocobranca/planos-cobranca.service';

/**
 * As listas de cadastro são configuração declarativa em cima da tabela comum; o que importa conferir é o que a PESSOA lê em cada linha
 * (máscara de CPF/CNPJ, moeda, "3 aluguéis", status) e para onde "Novo" e "Editar" levam.
 */
interface CasoDeLista {
  nome: string;
  componente: Type<unknown>;
  servico: Type<unknown>;
  linhas: unknown[];
  /** textos que precisam aparecer na linha, já formatados para quem lê */
  textosDaPrimeiraLinha: string[];
  rota: string;
}

const NBSP = / /g;   // o Intl separa "R$" do valor com espaço sem quebra

const CASOS: CasoDeLista[] = [
  {
    nome: 'parceiros', componente: ParceirosListComponent, servico: ParceirosService, rota: '/parceiros',
    linhas: [{ id: 'p1', nome: 'Hotel Central', cnpj: '11222333000181', categoria: 'Hotel', ativo: true }],
    textosDaPrimeiraLinha: ['Hotel Central', '11.222.333/0001-81', 'Hotel', 'Ativo']
  },
  {
    nome: 'planos de cobrança', componente: PlanosCobrancaListComponent, servico: PlanosCobrancaService, rota: '/planos-cobranca',
    linhas: [{ id: 'pl1', nome: 'Plano SUV', grupoAutomovelNome: 'SUV', precoDiaria: 150, precoPorKm: 2.5, kmLivreLimite: 200 }],
    textosDaPrimeiraLinha: ['Plano SUV', 'SUV', 'R$ 150,00', 'R$ 2,50', '200']
  },
  {
    nome: 'grupos de veículo', componente: GruposVeiculoListComponent, servico: GruposVeiculoService, rota: '/grupoveiculos',
    linhas: [{ id: 'g1', nome: 'Econômico', descricao: 'Carros de entrada' }],
    textosDaPrimeiraLinha: ['Econômico', 'Carros de entrada']
  },
  {
    nome: 'desafios de cupom', componente: DesafiosCupomListComponent, servico: DesafiosCupomService, rota: '/desafios-cupom',
    linhas: [{ id: 'd1', nome: 'Cliente fiel', descricao: 'Alugue mais', metaQuantidadeAlugueis: 3, periodoDias: 60, cupomRecompensaCodigo: 'FIEL10', ativo: false }],
    textosDaPrimeiraLinha: ['Cliente fiel', '3 aluguéis', '60 dias', 'FIEL10', 'Inativo']
  },
  {
    nome: 'funcionários', componente: FuncionariosListComponent, servico: FuncionariosService, rota: '/funcionarios',
    linhas: [{ id: 'f1', nomeCompleto: 'Pedro Atendente', cpf: '52998224725', email: 'pedro@locadora.com', salario: 3200, admissaoEmUtc: '2026-03-10' }],
    textosDaPrimeiraLinha: ['Pedro Atendente', '529.982.247-25', 'R$ 3.200,00', '10/03/2026']
  },
  {
    nome: 'cupons', componente: CuponsListComponent, servico: CuponsService, rota: '/cupons',
    linhas: [{ id: 'c1', codigo: 'DEZ', descricao: 'Dez por cento', tipoDesconto: 'Percentual', valorDesconto: 10, validoAte: '2030-12-31T00:00:00Z', limiteUsos: 5, usosAtuais: 2, ativo: true }],
    textosDaPrimeiraLinha: ['DEZ', '10%', '2 / 5', 'Ativo']
  }
];

describe('Listas de cadastro — o que a pessoa lê e para onde as ações levam', () => {
  for (const caso of CASOS) {
    describe(caso.nome, () => {
      const montar = () => {
        const svc = jasmine.createSpyObj('Servico', ['listar', 'excluir']);
        svc.listar.and.returnValue(of(caso.linhas));
        TestBed.configureTestingModule({
          providers: [
            provideRouter([]),
            { provide: caso.servico, useValue: svc },
            { provide: AuthService, useValue: { getUsuario: () => ({ cargo: 'Empresa' }) } },
            { provide: NotificacaoService, useValue: criarNotificacaoFalsa() }
          ]
        });
        const router = TestBed.inject(Router);
        spyOn(router, 'navigate').and.resolveTo(true);
        const fixture = TestBed.createComponent(caso.componente);
        fixture.detectChanges();
        return { fixture, svc, router, componente: fixture.componentInstance as { acoes: { rotulo: string; executar: (l: unknown) => void }[]; novo(): void } };
      };

      it('carrega a lista pelo serviço e mostra cada valor já formatado', () => {
        const { fixture, svc } = montar();

        const linha = (fixture.nativeElement as HTMLElement).querySelector('tbody tr')?.textContent?.replace(NBSP, ' ') ?? '';

        expect(svc.listar).toHaveBeenCalledTimes(1);
        for (const texto of caso.textosDaPrimeiraLinha) expect(linha).withContext(texto).toContain(texto);
      });

      it('"Novo" abre o formulário de cadastro', () => {
        const { componente, router } = montar();

        componente.novo();

        expect(router.navigate).toHaveBeenCalledOnceWith([`${caso.rota}/new`]);
      });

      it('"Editar" na linha abre o formulário de edição daquele registro', () => {
        const { componente, router } = montar();
        const editar = componente.acoes.find(a => a.rotulo === 'Editar');

        editar?.executar(caso.linhas[0]);

        expect(router.navigate).toHaveBeenCalledOnceWith([`${caso.rota}/${(caso.linhas[0] as { id: string }).id}/edit`]);
      });

      it('oferece Editar e Excluir na linha', () => {
        const { componente } = montar();

        expect(componente.acoes.map(a => a.rotulo)).toEqual(['Editar', 'Excluir']);
      });
    });
  }
});

describe('CuponsListComponent — uso do cupom', () => {
  const montar = (cupom: object) => {
    const svc = jasmine.createSpyObj('CuponsService', ['listar', 'excluir']);
    svc.listar.and.returnValue(of([{ id: 'c1', codigo: 'LIVRE', descricao: 'd', tipoDesconto: 'ValorFixo', valorDesconto: 25, validoAte: '2030-12-31T00:00:00Z', ativo: true, ...cupom }]));
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: CuponsService, useValue: svc }, { provide: NotificacaoService, useValue: criarNotificacaoFalsa() }]
    });
    const fixture = TestBed.createComponent(CuponsListComponent);
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement).querySelector('tbody tr')?.textContent?.replace(NBSP, ' ') ?? '';
  };

  it('cupom de valor fixo mostra o desconto em reais', () => {
    expect(montar({ limiteUsos: 10, usosAtuais: 0 })).toContain('R$ 25,00');
  });

  it('cupom sem limite de usos não mostra "null": diz quantas vezes já foi usado e que não tem limite', () => {
    const linha = montar({ limiteUsos: null, usosAtuais: 3 });

    expect(linha).not.toContain('null');
    expect(linha).toContain('3');
    expect(linha.toLowerCase()).toContain('sem limite');
  });
});

describe('CondutoresListComponent — situação da CNH e filtro por cliente', () => {
  const ontem = new Date(Date.now() - 86_400_000).toISOString();
  const amanha = new Date(Date.now() + 86_400_000 * 30).toISOString();

  const condutores = [
    { id: 'k1', nome: 'Ana', cpf: '52998224725', cnh: '111', validadeCnh: amanha, telefone: '(11) 9', email: 'a@a.com', clienteId: 'cli1', clienteNome: 'Cliente 1' },
    { id: 'k2', nome: 'Bruno', cpf: '11144477735', cnh: '222', validadeCnh: ontem, telefone: '(11) 9', email: 'b@b.com', clienteId: 'cli2', clienteNome: 'Cliente 2' }
  ];

  const montar = () => {
    const svc = jasmine.createSpyObj('CondutoresService', ['listar', 'excluir']);
    svc.listar.and.returnValue(of(condutores));
    const clientes = jasmine.createSpyObj('ClientesService', ['listar']);
    clientes.listar.and.returnValue(of([{ id: 'cli1', nome: 'Cliente 1' }, { id: 'cli2', nome: 'Cliente 2' }]));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: CondutoresService, useValue: svc },
        { provide: ClientesService, useValue: clientes },
        { provide: NotificacaoService, useValue: criarNotificacaoFalsa() }
      ]
    });
    const fixture = TestBed.createComponent(CondutoresListComponent);
    fixture.detectChanges();
    return fixture;
  };

  const linhas = (f: ReturnType<typeof montar>) =>
    Array.from((f.nativeElement as HTMLElement).querySelectorAll('tbody tr')).map(l => l.textContent?.replace(NBSP, ' ') ?? '');

  it('marca a CNH vencida e a em dia com selos diferentes, e formata o CPF', () => {
    const f = montar();

    const [ana, bruno] = linhas(f);

    expect(ana).toContain('Em dia');
    expect(ana).toContain('529.982.247-25');
    expect(bruno).toContain('Vencida');
  });

  it('CNH que vence hoje ainda vale (só vencida a partir do dia seguinte)', () => {
    const f = montar();
    const hoje = { ...condutores[0], validadeCnh: new Date().toISOString() };

    expect(f.componentInstance.cnhVencida(hoje as never)).toBeFalse();
    expect(f.componentInstance.cnhVencida({ ...condutores[0], validadeCnh: ontem } as never)).toBeTrue();
    expect(f.componentInstance.cnhVencida({ ...condutores[0], validadeCnh: 'data inválida' } as never)).toBeFalse();
  });

  it('filtrar por cliente mostra só os condutores dele; "todos" volta a mostrar todos', () => {
    const f = montar();

    f.componentInstance.filtrarPorCliente('cli2');
    f.detectChanges();
    expect(linhas(f).length).toBe(1);
    expect(linhas(f)[0]).toContain('Bruno');

    f.componentInstance.filtrarPorCliente('');
    f.detectChanges();
    expect(linhas(f).length).toBe(2);
  });
});
