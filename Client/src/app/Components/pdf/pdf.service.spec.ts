import { TestBed } from '@angular/core/testing';
import { jsPDF } from 'jspdf';
import { PdfService } from './pdf.service';
import { AluguelCompletoDto, SelecionarAlugueisDto } from '../alugueis/aluguel.model';
import { DevolucaoCompletoDto, SelecionarDevolucoesDto } from '../devolucoes/devolucao.model';

describe('PdfService — relatórios em lista', () => {
  let service: PdfService;

  // O jsPDF guarda o conteúdo de cada página como operadores PDF em texto: dá para conferir o que foi escrito.
  // Parênteses e barras saem escapados no fluxo do PDF ("aluguel\(éis\)"); desfaz para comparar texto legível.
  const conteudo = (doc: jsPDF) =>
    (doc.internal as unknown as { pages: (string[] | undefined)[] }).pages.flatMap(p => p ?? []).join('\n')
      .replace(/\\([()\\])/g, '$1');

  const aluguel = (placa: string, status: string, valor: number): SelecionarAlugueisDto => ({
    id: placa, condutorNome: 'Joana Condutora', automovelPlaca: placa, clienteNome: 'Cliente Teste',
    dataSaida: '2026-10-10T00:00:00Z', dataRetornoPrevisto: '2026-10-13T00:00:00Z', valorPrevisto: valor, status
  });

  const devolucao = (placa: string, valor: number): SelecionarDevolucoesDto => ({
    id: placa, condutorNome: 'Joana Condutora', automovelPlaca: placa, clienteNome: 'Cliente Teste',
    dataDevolucao: '2026-10-13T00:00:00Z', quilometragemFinal: 15000, valorTotal: valor
  });

  beforeEach(() => {
    service = TestBed.inject(PdfService);
  });

  it('relatório de aluguéis: título, filtro, uma linha por aluguel e totais', () => {
    const doc = service.montarRelatorioAlugueis(
      [aluguel('ABC1D23', 'EmAndamento', 300), aluguel('XYZ9Z99', 'Reservado', 450.5)], 'Em andamento');

    const texto = conteudo(doc);
    expect(texto).toContain('Relatório de aluguéis');
    expect(texto).toContain('Filtro: Em andamento');
    expect(texto).toContain('ABC-1D23');
    expect(texto).toContain('XYZ-9Z99');
    expect(texto).toContain('Em andamento'); // status traduzido
    expect(texto).toContain('2 aluguel(éis)');
    expect(texto).toContain('750,50');          // soma dos valores previstos
    expect(texto).toContain('Página 1 de 1');
  });

  it('relatório de aluguéis é paisagem (7 colunas cabem)', () => {
    const doc = service.montarRelatorioAlugueis([aluguel('ABC1D23', 'Reservado', 100)], 'Todos');

    expect(doc.internal.pageSize.getWidth()).toBeGreaterThan(doc.internal.pageSize.getHeight());
  });

  it('lista vazia gera o relatório com total zero em vez de falhar', () => {
    const doc = service.montarRelatorioAlugueis([], 'Cancelados');

    expect(conteudo(doc)).toContain('0 aluguel(éis)');
    expect(doc.getNumberOfPages()).toBe(1);
  });

  it('muitos registros viram várias páginas, todas numeradas "Página x de y"', () => {
    const muitos = Array.from({ length: 80 }, (_, i) => aluguel(`AAA${String(i).padStart(4, '0')}`, 'Concluido', 100));

    const doc = service.montarRelatorioAlugueis(muitos, 'Todos');
    const total = doc.getNumberOfPages();

    expect(total).toBeGreaterThan(1);
    expect(conteudo(doc)).toContain(`Página 1 de ${total}`);
    expect(conteudo(doc)).toContain(`Página ${total} de ${total}`);
  });

  it('relatório de devoluções: linhas, quilometragem e total', () => {
    const doc = service.montarRelatorioDevolucoes([devolucao('ABC1D23', 1200), devolucao('XYZ9Z99', 800.25)]);

    const texto = conteudo(doc);
    expect(texto).toContain('Relatório de devoluções');
    expect(texto).toContain('ABC-1D23');
    expect(texto).toContain('15000 km');
    expect(texto).toContain('2 devolução(ões)');
    expect(texto).toContain('2.000,25');
  });

  it('gerarRelatorioAlugueis/Devolucoes baixam o arquivo com a data no nome', () => {
    const salvar = spyOn(service as unknown as { baixar: (doc: jsPDF, nome: string) => void }, 'baixar').and.stub();

    service.gerarRelatorioAlugueis([aluguel('ABC1D23', 'Reservado', 100)], 'Todos');
    service.gerarRelatorioDevolucoes([devolucao('ABC1D23', 100)]);

    const hoje = new Date().toISOString().slice(0, 10);
    expect(salvar.calls.argsFor(0)[1]).toBe(`relatorio-alugueis-${hoje}.pdf`);
    expect(salvar.calls.argsFor(1)[1]).toBe(`relatorio-devolucoes-${hoje}.pdf`);
    expect((salvar.calls.argsFor(0)[0] as jsPDF).getNumberOfPages()).toBe(1);
  });

  // ---- Identidade visual: os PDFs seguem o tema do app (cores, selo de status, cartões, rodapé) ----

  it('usa as cores do tema claro do app: primária no marca/títulos e container primário nos totais', () => {
    const texto = conteudo(service.montarRelatorioAlugueis([aluguel('ABC1D23', 'Reservado', 100)], 'Todos'));

    // operadores de cor do PDF em 0..1, com 2 casas: #6750a4 (primária) e #eaddff (container da primária)
    expect(texto).toContain('0.4 0.31 0.64 rg');
    expect(texto).toMatch(/0\.92 0\.87 1\.? rg/);
  });

  it('o status ganha o selo da cor correspondente (como na tela): andamento = alerta, concluído = sucesso, cancelado = erro', () => {
    const doc = service.montarRelatorioAlugueis([
      aluguel('AAA1A11', 'EmAndamento', 100), aluguel('BBB2B22', 'Concluido', 100), aluguel('CCC3C33', 'Cancelado', 100)
    ], 'Todos');
    const texto = conteudo(doc);

    expect(texto).toMatch(/1\.? 0\.87 0\.72 rg/);     // #ffddb8 container de alerta
    expect(texto).toMatch(/0\.72 0\.95 0\.77 rg/); // #b7f2c5 container de sucesso
    expect(texto).toMatch(/0\.98 0\.87 0\.86 rg/); // #f9dedc container de erro
  });

  it('os relatórios têm cartões de resumo antes da tabela', () => {
    const alugueis = service.montarRelatorioAlugueis([aluguel('ABC1D23', 'EmAndamento', 300)], 'Todos');
    const devolucoes = service.montarRelatorioDevolucoes([devolucao('ABC1D23', 1200), devolucao('XYZ9Z99', 800)]);

    expect(conteudo(alugueis)).toContain('Aluguéis na lista');
    expect(conteudo(alugueis)).toContain('Valor previsto total');
    expect(conteudo(devolucoes)).toContain('Devoluções na lista');
    expect(conteudo(devolucoes)).toContain('Valor médio por devolução');
    expect(conteudo(devolucoes)).toContain('1.000,00'); // média de 1.200 e 800
  });

  it('o rodapé identifica a locadora e numera as páginas à direita', () => {
    const texto = conteudo(service.montarRelatorioDevolucoes([devolucao('ABC1D23', 100)]));

    expect(texto).toContain('Locadora de Veículos');
    expect(texto).toContain('Página 1 de 1');
  });
});

describe('PdfService — recibo de devolução', () => {
  let service: PdfService;

  const conteudo = (doc: jsPDF) =>
    (doc.internal as unknown as { pages: (string[] | undefined)[] }).pages.flatMap(p => p ?? []).join('\n')
      .replace(/\\([()\\])/g, '$1');

  const devolucao: DevolucaoCompletoDto = {
    id: 'dev-123', aluguelId: 'alu-1', condutorNome: 'Joana Condutora', automovelPlaca: 'ABC1D23', clienteNome: 'Cliente Teste',
    dataDevolucao: '2026-10-13T00:00:00Z', quilometragemFinal: 15000, combustivelNoTanque: 40, nivelCombustivel: 'TresQuartos',
    valorMultas: 50, valorAdicionalCombustivel: 25, valorTotal: 375
  };
  const aluguel: AluguelCompletoDto = {
    id: 'alu-1', condutorId: 'c', automovelId: 'a', clienteId: 'cl', dataSaida: '2026-10-10T00:00:00Z',
    dataRetornoPrevisto: '2026-10-13T00:00:00Z', valorPrevisto: 300, valorCaucao: 500, status: 'Concluido', taxasServicosIds: [],
    taxasServicos: [{ id: 't1', nome: 'GPS', preco: 20, tipoCalculo: 'Diario' }]
  };

  beforeEach(() => {
    service = TestBed.inject(PdfService);
  });

  it('traz título, dados do aluguel e da devolução, placa formatada e valores em reais', () => {
    const texto = conteudo(service.montarReciboDevolucao(devolucao, aluguel));

    for (const esperado of [
      'Recibo de devolução', 'Joana Condutora', 'ABC-1D23', 'Cliente Teste', '15000 km', '40 L', 'Três quartos',
      'Detalhes financeiros', 'Valor da caução'
    ]) {
      expect(texto).withContext(esperado).toContain(esperado);
    }
    expect(texto).toContain('R$'); // moeda pt-BR
    expect(texto).toContain('875,00'); // total = valor total da devolução (375) + caução (500)
  });

  it('cabe numa página retrato A4 e tem as duas assinaturas e o código da devolução', () => {
    const doc = service.montarReciboDevolucao(devolucao, aluguel);
    const texto = conteudo(doc);

    expect(doc.getNumberOfPages()).toBe(1);
    expect(doc.internal.pageSize.getHeight()).toBeGreaterThan(doc.internal.pageSize.getWidth());
    expect(texto).toContain('Assinatura do funcionário');
    expect(texto).toContain('Assinatura do cliente');
    expect(texto).toContain('Código da devolução: dev-123');
    expect(texto).toContain('Página 1 de 1');
  });

  it('gerarReciboDevolucao baixa o arquivo com o código e a data no nome', () => {
    const salvar = spyOn(service as unknown as { baixar: (doc: jsPDF, nome: string) => void }, 'baixar').and.stub();

    service.gerarReciboDevolucao(devolucao, aluguel);

    expect(salvar.calls.argsFor(0)[1]).toMatch(/^recibo-devolucao-dev-123-\d{2}-\d{2}-\d{4}\.pdf$/);
  });
});
