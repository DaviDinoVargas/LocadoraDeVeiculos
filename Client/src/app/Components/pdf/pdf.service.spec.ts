import { TestBed } from '@angular/core/testing';
import { jsPDF } from 'jspdf';
import { PdfService } from './pdf.service';
import { SelecionarAlugueisDto } from '../alugueis/aluguel.model';
import { SelecionarDevolucoesDto } from '../devolucoes/devolucao.model';

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
    expect(texto).toContain('ABC1D23');
    expect(texto).toContain('XYZ9Z99');
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
    expect(texto).toContain('ABC1D23');
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
});
