import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { SILENCIAR_ERRO } from './http-context';
import { AlugueisService } from '../Components/alugueis/alugueis.service';
import { AuditoriaService } from '../Components/auditoria/auditoria.service';
import { ClientesService } from '../Components/clientes/clientes.service';
import { CondutoresService } from '../Components/condutor/condutores.service';
import { ConfiguracoesService } from '../Components/configuracoes/configuracoes.service';
import { CuponsService } from '../Components/cupons/cupons.service';
import { DesafiosCupomService } from '../Components/desafios-cupom/desafios-cupom.service';
import { DevolucoesService } from '../Components/devolucoes/devolucoes.service';
import { FuncionariosService } from '../Components/funcionarios/funcionarios.service';
import { GruposVeiculoService } from '../Components/grupoveiculos/grupos-veiculo.service';
import { DashboardService } from '../Components/home/dashboard.service';
import { ParceirosService } from '../Components/parceiros/parceiros.service';
import { PlanosCobrancaService } from '../Components/planocobranca/planos-cobranca.service';
import { TaxasServicosService } from '../Components/taxa-servico/taxas-servicos.service';
import { VeiculosService } from '../Components/veiculos/veiculos.service';

/**
 * Contrato entre o front e a Web API: cada método dos serviços precisa chamar o VERBO e a ROTA que o controller correspondente expõe
 * (as rotas abaixo espelham os controllers de Server/web-api/Controllers). Uma rota que mude de um lado só deixa a tela quebrada sem
 * que nenhum teste de componente perceba, porque eles dublam o serviço inteiro. Aqui o serviço é de verdade e só o HTTP é falso.
 */
interface Chamada {
  nome: string;
  chamar: () => Observable<unknown>;
  verbo: 'GET' | 'POST' | 'PUT' | 'DELETE';
  url: string;
  corpo?: unknown;
}

const ID = '3f6b1c2e-9d4a-4c1e-8b7a-1234567890ab';
const OUTRO = '9a8b7c6d-5e4f-4a3b-8c2d-0f1e2d3c4b5a';

/** O CRUD padrão (lista, por id, criar, atualizar, excluir) que quase todo serviço repete. */
const crud = (nome: string, servico: any, rota: string): Chamada[] => [
  { nome: `${nome}.listar`, chamar: () => servico().listar(), verbo: 'GET', url: `/api/${rota}` },
  { nome: `${nome}.obter`, chamar: () => servico().obter(ID), verbo: 'GET', url: `/api/${rota}/${ID}` },
  { nome: `${nome}.criar`, chamar: () => servico().criar({ x: 1 }), verbo: 'POST', url: `/api/${rota}`, corpo: { x: 1 } },
  { nome: `${nome}.atualizar`, chamar: () => servico().atualizar(ID, { x: 2 }), verbo: 'PUT', url: `/api/${rota}/${ID}`, corpo: { x: 2 } },
  { nome: `${nome}.excluir`, chamar: () => servico().excluir(ID), verbo: 'DELETE', url: `/api/${rota}/${ID}` }
];

describe('Contrato do front com a Web API (verbo e rota de cada serviço)', () => {
  let http: HttpTestingController;
  const obter = <T>(tipo: new () => T) => TestBed.inject(tipo);

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const chamadas = (): Chamada[] => [
    ...crud('grupos', () => obter(GruposVeiculoService), 'grupos-automovel'),
    ...crud('planos', () => obter(PlanosCobrancaService), 'planos-cobranca'),
    ...crud('veículos', () => obter(VeiculosService), 'automoveis'),
    ...crud('condutores', () => obter(CondutoresService), 'condutores'),
    ...crud('taxas', () => obter(TaxasServicosService), 'taxas-servicos'),
    ...crud('cupons', () => obter(CuponsService), 'cupons'),
    ...crud('parceiros', () => obter(ParceirosService), 'parceiros'),
    ...crud('desafios', () => obter(DesafiosCupomService), 'desafios-cupom'),
    ...crud('funcionários', () => obter(FuncionariosService), 'funcionarios'),

    // aluguéis
    { nome: 'aluguéis.listar', chamar: () => obter(AlugueisService).listar(), verbo: 'GET', url: '/api/alugueis' },
    { nome: 'aluguéis.listarPorStatus', chamar: () => obter(AlugueisService).listarPorStatus('EmAndamento'), verbo: 'GET', url: '/api/alugueis/status/EmAndamento' },
    { nome: 'aluguéis.selecionarEmAberto', chamar: () => obter(AlugueisService).selecionarEmAberto(), verbo: 'GET', url: '/api/alugueis/em-aberto' },
    { nome: 'aluguéis.obter', chamar: () => obter(AlugueisService).obter(ID), verbo: 'GET', url: `/api/alugueis/${ID}` },
    { nome: 'aluguéis.criar', chamar: () => obter(AlugueisService).criar({ a: 1 } as never), verbo: 'POST', url: '/api/alugueis', corpo: { a: 1 } },
    { nome: 'aluguéis.atualizar', chamar: () => obter(AlugueisService).atualizar(ID, { a: 2 } as never), verbo: 'PUT', url: `/api/alugueis/${ID}`, corpo: { a: 2 } },
    { nome: 'aluguéis.iniciar', chamar: () => obter(AlugueisService).iniciar(ID), verbo: 'POST', url: `/api/alugueis/${ID}/iniciar`, corpo: {} },
    { nome: 'aluguéis.cancelar', chamar: () => obter(AlugueisService).cancelar(ID), verbo: 'POST', url: `/api/alugueis/${ID}/cancelar`, corpo: {} },
    { nome: 'aluguéis.excluir', chamar: () => obter(AlugueisService).excluir(ID), verbo: 'DELETE', url: `/api/alugueis/${ID}` },
    {
      nome: 'aluguéis.simular',
      chamar: () => obter(AlugueisService).simular({ automovelId: ID, dataSaida: 'a', dataRetornoPrevisto: 'b', taxasServicosIds: [] }),
      verbo: 'POST', url: '/api/alugueis/simular', corpo: { automovelId: ID, dataSaida: 'a', dataRetornoPrevisto: 'b', taxasServicosIds: [] }
    },

    // veículos (extras)
    {
      nome: 'veículos.listarDisponiveis (cria aluguel)', chamar: () => obter(VeiculosService).listarDisponiveis('2030-01-01', '2030-01-04'),
      verbo: 'GET', url: '/api/automoveis/disponiveis?dataSaida=2030-01-01&dataRetornoPrevisto=2030-01-04'
    },
    {
      nome: 'veículos.listarDisponiveis (edita aluguel: o próprio aluguel não bloqueia o carro)',
      chamar: () => obter(VeiculosService).listarDisponiveis('2030-01-01', '2030-01-04', ID),
      verbo: 'GET', url: `/api/automoveis/disponiveis?dataSaida=2030-01-01&dataRetornoPrevisto=2030-01-04&aluguelId=${ID}`
    },
    { nome: 'veículos.listarPorGrupo', chamar: () => obter(VeiculosService).listarPorGrupo(OUTRO), verbo: 'GET', url: `/api/automoveis/grupo/${OUTRO}` },

    // clientes
    { nome: 'clientes.listar', chamar: () => obter(ClientesService).listar(), verbo: 'GET', url: '/api/clientes' },
    { nome: 'clientes.listarPessoasFisicas', chamar: () => obter(ClientesService).listarPessoasFisicas(), verbo: 'GET', url: '/api/clientes/pessoas-fisicas' },
    { nome: 'clientes.listarPessoasJuridicas', chamar: () => obter(ClientesService).listarPessoasJuridicas(), verbo: 'GET', url: '/api/clientes/pessoas-juridicas' },
    { nome: 'clientes.obter', chamar: () => obter(ClientesService).obter(ID), verbo: 'GET', url: `/api/clientes/${ID}` },
    { nome: 'clientes.criarPessoaFisica', chamar: () => obter(ClientesService).criarPessoaFisica({ c: 1 } as never), verbo: 'POST', url: '/api/clientes/pessoa-fisica', corpo: { c: 1 } },
    { nome: 'clientes.criarPessoaJuridica', chamar: () => obter(ClientesService).criarPessoaJuridica({ c: 2 } as never), verbo: 'POST', url: '/api/clientes/pessoa-juridica', corpo: { c: 2 } },
    { nome: 'clientes.atualizarPessoaFisica', chamar: () => obter(ClientesService).atualizarPessoaFisica(ID, { c: 3 } as never), verbo: 'PUT', url: `/api/clientes/pessoa-fisica/${ID}`, corpo: { c: 3 } },
    { nome: 'clientes.atualizarPessoaJuridica', chamar: () => obter(ClientesService).atualizarPessoaJuridica(ID, { c: 4 } as never), verbo: 'PUT', url: `/api/clientes/pessoa-juridica/${ID}`, corpo: { c: 4 } },
    { nome: 'clientes.excluir', chamar: () => obter(ClientesService).excluir(ID), verbo: 'DELETE', url: `/api/clientes/${ID}` },

    // condutores, taxas, cupons e desafios (extras)
    { nome: 'condutores.listarPorCliente', chamar: () => obter(CondutoresService).listarPorCliente(OUTRO), verbo: 'GET', url: `/api/condutores/cliente/${OUTRO}` },
    { nome: 'taxas.listarPorTipo', chamar: () => obter(TaxasServicosService).listarPorTipo('Fixo'), verbo: 'GET', url: '/api/taxas-servicos/tipo/Fixo' },
    { nome: 'cupons.validar', chamar: () => obter(CuponsService).validar('DEZ', 300), verbo: 'POST', url: '/api/cupons/validar', corpo: { codigo: 'DEZ', valorBase: 300 } },
    { nome: 'desafios.progressoDoCliente', chamar: () => obter(DesafiosCupomService).progressoDoCliente(OUTRO), verbo: 'GET', url: `/api/desafios-cupom/cliente/${OUTRO}/progresso` },

    // devoluções
    { nome: 'devoluções.listar', chamar: () => obter(DevolucoesService).listar(), verbo: 'GET', url: '/api/devolucoes' },
    { nome: 'devoluções.obter', chamar: () => obter(DevolucoesService).obter(ID), verbo: 'GET', url: `/api/devolucoes/${ID}` },
    { nome: 'devoluções.obterPorAluguelId', chamar: () => obter(DevolucoesService).obterPorAluguelId(OUTRO), verbo: 'GET', url: `/api/devolucoes/aluguel/${OUTRO}` },
    { nome: 'devoluções.registrar', chamar: () => obter(DevolucoesService).registrar({ d: 1 } as never), verbo: 'POST', url: '/api/devolucoes', corpo: { d: 1 } },
    { nome: 'devoluções.excluir', chamar: () => obter(DevolucoesService).excluir(ID), verbo: 'DELETE', url: `/api/devolucoes/${ID}` },

    // configurações, painel e auditoria
    { nome: 'configurações.obter', chamar: () => obter(ConfiguracoesService).obter(), verbo: 'GET', url: '/api/configuracoes' },
    { nome: 'configurações.atualizarValorGarantia', chamar: () => obter(ConfiguracoesService).atualizarValorGarantia(750), verbo: 'PUT', url: '/api/configuracoes/valor-garantia', corpo: { valorGarantia: 750 } },
    { nome: 'configurações.atualizarPrecoCombustivel', chamar: () => obter(ConfiguracoesService).atualizarPrecoCombustivel(6.25), verbo: 'PUT', url: '/api/configuracoes/preco-combustivel', corpo: { precoCombustivel: 6.25 } },
    { nome: 'painel.resumo', chamar: () => obter(DashboardService).resumo(), verbo: 'GET', url: '/api/dashboard/resumo' },
    { nome: 'auditoria.listar', chamar: () => obter(AuditoriaService).listar(50), verbo: 'GET', url: '/api/auditoria?limite=50' }
  ];

  it('cada método chama o verbo e a rota que o controller expõe, com o corpo certo', () => {
    for (const c of chamadas()) {
      let resposta: unknown;
      c.chamar().subscribe(r => (resposta = r));

      const req = http.expectOne(r => r.urlWithParams === c.url, `${c.nome}: ${c.verbo} ${c.url}`);
      expect(req.request.method).withContext(c.nome).toBe(c.verbo);
      if (c.corpo !== undefined) expect(req.request.body).withContext(`${c.nome}: corpo`).toEqual(c.corpo);
      req.flush({});
      expect(resposta).withContext(`${c.nome}: respondeu`).toBeDefined();
    }
  });

  it('as listas vêm da propriedade "registros" da resposta e os objetos vêm direto', () => {
    let lista: unknown;
    let um: unknown;
    obter(ClientesService).listar().subscribe((r: unknown) => (lista = r));
    obter(ClientesService).obter(ID).subscribe((r: unknown) => (um = r));

    http.expectOne('/api/clientes').flush({ registros: [{ id: 'a' }, { id: 'b' }] });
    http.expectOne(`/api/clientes/${ID}`).flush({ id: ID, nome: 'Maria' });

    expect(lista).toEqual([{ id: 'a' }, { id: 'b' }]);
    expect(um).toEqual({ id: ID, nome: 'Maria' });
  });

  it('as consultas que a tela repete a cada tecla ou ciclo não abrem aviso de erro a cada falha', () => {
    obter(AlugueisService).simular({ automovelId: ID, dataSaida: 'a', dataRetornoPrevisto: 'b' }).subscribe({ error: () => undefined });
    obter(CuponsService).validar('DEZ', 100).subscribe({ error: () => undefined });
    obter(DashboardService).resumo().subscribe({ error: () => undefined });

    const requisicoes = http.match(() => true);
    expect(requisicoes.length).toBe(3);
    expect(requisicoes.every(r => r.request.context.get(SILENCIAR_ERRO))).withContext('simular, validar cupom e painel marcam SILENCIAR_ERRO').toBeTrue();
  });
});
