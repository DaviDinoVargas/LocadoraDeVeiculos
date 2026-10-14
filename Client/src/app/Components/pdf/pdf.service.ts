import { Injectable } from '@angular/core';
import { jsPDF } from 'jspdf';
import autoTable from 'jspdf-autotable';
import { AluguelCompletoDto, SelecionarAlugueisDto } from '../alugueis/aluguel.model';
import { DevolucaoCompletoDto, SelecionarDevolucoesDto } from '../devolucoes/devolucao.model';
import { mascararPlaca } from '../../shared/mascaras/mascara.directive';

@Injectable({
  providedIn: 'root'
})
export class PdfService {

  gerarReciboDevolucao(devolucao: DevolucaoCompletoDto, aluguel: AluguelCompletoDto): void {
    const doc = new jsPDF();
    const dataAtual = new Date().toLocaleDateString('pt-BR');
    const horaAtual = new Date().toLocaleTimeString('pt-BR');

    // Cabeçalho
    doc.setFontSize(20);
    doc.text('RECIBO DE DEVOLUÇÃO', 105, 20, { align: 'center' });

    doc.setFontSize(12);
    doc.text('Locadora de Veículos', 105, 30, { align: 'center' });
    doc.text(`Data: ${dataAtual} ${horaAtual}`, 105, 40, { align: 'center' });
    doc.text('----------------------------------------------------', 105, 45, { align: 'center' });

    // Informações do Aluguel
    doc.setFontSize(14);
    doc.text('Informações do Aluguel', 20, 60);

    doc.setFontSize(10);
    let y = 70;

    doc.text(`Devolução: ${devolucao.id}`, 20, y);
    y += 8;
    doc.text(`Condutor: ${devolucao.condutorNome}`, 20, y);
    y += 8;
    doc.text(`Veículo: ${mascararPlaca(devolucao.automovelPlaca)}`, 20, y);
    y += 8;
    doc.text(`Cliente: ${devolucao.clienteNome}`, 20, y);
    y += 8;
    doc.text(`Data Saída: ${this.formatarData(aluguel.dataSaida)}`, 20, y);
    y += 8;
    doc.text(`Data Retorno Previsto: ${this.formatarData(aluguel.dataRetornoPrevisto)}`, 20, y);
    y += 8;
    doc.text(`Valor Previsto: ${this.formatarMoeda(aluguel.valorPrevisto)}`, 20, y);

    y += 12;

    // Informações da Devolução
    doc.setFontSize(14);
    doc.text('Informações da Devolução', 20, y);

    doc.setFontSize(10);
    y += 10;
    doc.text(`Data Devolução: ${this.formatarData(devolucao.dataDevolucao)}`, 20, y);
    y += 8;
    doc.text(`Quilometragem Final: ${devolucao.quilometragemFinal} km`, 20, y);
    y += 8;
    doc.text(`Combustível no Tanque: ${devolucao.combustivelNoTanque} L`, 20, y);
    y += 8;
    doc.text(`Nível Combustível: ${this.getNivelCombustivelText(devolucao.nivelCombustivel)}`, 20, y);

    y += 12;

    // Detalhes Financeiros
    doc.setFontSize(14);
    doc.text('Detalhes Financeiros', 20, y);

    // Tabela de valores
    const tableData = [
      ['Valor do Aluguel', this.formatarMoeda(aluguel.valorPrevisto)],
      ['Taxas e Serviços', this.formatarMoeda(this.calcularTaxas(aluguel))],
      ['Multas', this.formatarMoeda(devolucao.valorMultas)],
      ['Adicional Combustível', this.formatarMoeda(devolucao.valorAdicionalCombustivel)],
      ['Valor da Caução', this.formatarMoeda(aluguel.valorCaucao)],
      ['', ''],
      ['TOTAL', this.formatarMoeda(devolucao.valorTotal + (aluguel.valorCaucao ?? 0))]
    ];

    autoTable(doc, {
      startY: y + 10,
      head: [['Descrição', 'Valor']],
      body: tableData,
      theme: 'grid',
      headStyles: { fillColor: [41, 128, 185], textColor: 255 },
      margin: { left: 20, right: 20 }
    });

    // Rodapé
    const finalY = (doc as jsPDF & { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 20;
    doc.text('----------------------------------------------------', 105, finalY, { align: 'center' });
    doc.text('Assinatura do Funcionário', 105, finalY + 10, { align: 'center' });
    doc.text('_________________________', 105, finalY + 25, { align: 'center' });
    doc.text('Assinatura do Cliente', 105, finalY + 40, { align: 'center' });
    doc.text('_________________________', 105, finalY + 55, { align: 'center' });

    // Salvar PDF
    doc.save(`recibo-devolucao-${devolucao.id}-${dataAtual.replace(/\//g, '-')}.pdf`);
  }

  // ---- Relatórios em lista (exportam exatamente o que está na tela, já filtrado) ----------------

  /** Monta o relatório de aluguéis; separado de {@link gerarRelatorioAlugueis} para poder ser conferido sem baixar arquivo. */
  montarRelatorioAlugueis(alugueis: SelecionarAlugueisDto[], filtro: string): jsPDF {
    const doc = new jsPDF({ orientation: 'landscape' });
    this.cabecalhoRelatorio(doc, 'Relatório de aluguéis', `Filtro: ${filtro}`);

    autoTable(doc, {
      startY: 36,
      head: [['Condutor', 'Veículo', 'Cliente', 'Saída', 'Retorno previsto', 'Valor previsto', 'Status']],
      body: alugueis.map(a => [
        a.condutorNome, mascararPlaca(a.automovelPlaca), a.clienteNome,
        this.formatarData(a.dataSaida), this.formatarData(a.dataRetornoPrevisto),
        this.formatarMoeda(a.valorPrevisto), this.statusAluguelTexto(a.status)
      ]),
      foot: [[
        { content: `${alugueis.length} aluguel(éis)`, colSpan: 5 },
        { content: this.formatarMoeda(alugueis.reduce((s, a) => s + (a.valorPrevisto ?? 0), 0)), styles: { halign: 'right' } },
        ''
      ]],
      theme: 'grid',
      headStyles: { fillColor: [41, 128, 185], textColor: 255 },
      footStyles: { fillColor: [235, 238, 243], textColor: 20, fontStyle: 'bold' },
      columnStyles: { 5: { halign: 'right' } },
      margin: { left: 14, right: 14 }
    });

    this.rodapePaginas(doc);
    return doc;
  }

  gerarRelatorioAlugueis(alugueis: SelecionarAlugueisDto[], filtro: string): void {
    this.baixar(this.montarRelatorioAlugueis(alugueis, filtro), `relatorio-alugueis-${this.dataParaArquivo()}.pdf`);
  }

  montarRelatorioDevolucoes(devolucoes: SelecionarDevolucoesDto[]): jsPDF {
    const doc = new jsPDF({ orientation: 'landscape' });
    this.cabecalhoRelatorio(doc, 'Relatório de devoluções', 'Todas as devoluções registradas');

    autoTable(doc, {
      startY: 36,
      head: [['Condutor', 'Veículo', 'Cliente', 'Data da devolução', 'Km final', 'Valor total']],
      body: devolucoes.map(d => [
        d.condutorNome, mascararPlaca(d.automovelPlaca), d.clienteNome,
        this.formatarData(d.dataDevolucao), `${d.quilometragemFinal} km`, this.formatarMoeda(d.valorTotal)
      ]),
      foot: [[
        { content: `${devolucoes.length} devolução(ões)`, colSpan: 5 },
        { content: this.formatarMoeda(devolucoes.reduce((s, d) => s + (d.valorTotal ?? 0), 0)), styles: { halign: 'right' } }
      ]],
      theme: 'grid',
      headStyles: { fillColor: [41, 128, 185], textColor: 255 },
      footStyles: { fillColor: [235, 238, 243], textColor: 20, fontStyle: 'bold' },
      columnStyles: { 4: { halign: 'right' }, 5: { halign: 'right' } },
      margin: { left: 14, right: 14 }
    });

    this.rodapePaginas(doc);
    return doc;
  }

  gerarRelatorioDevolucoes(devolucoes: SelecionarDevolucoesDto[]): void {
    this.baixar(this.montarRelatorioDevolucoes(devolucoes), `relatorio-devolucoes-${this.dataParaArquivo()}.pdf`);
  }

  private cabecalhoRelatorio(doc: jsPDF, titulo: string, subtitulo: string): void {
    const agora = new Date();
    doc.setFontSize(16);
    doc.text(titulo, 14, 16);
    doc.setFontSize(10);
    doc.text('Locadora de Veículos', 14, 23);
    doc.text(subtitulo, 14, 29);
    doc.text(`Gerado em ${agora.toLocaleDateString('pt-BR')} às ${agora.toLocaleTimeString('pt-BR')}`,
      doc.internal.pageSize.getWidth() - 14, 23, { align: 'right' });
  }

  private rodapePaginas(doc: jsPDF): void {
    const total = doc.getNumberOfPages();
    for (let pagina = 1; pagina <= total; pagina++) {
      doc.setPage(pagina);
      doc.setFontSize(9);
      doc.text(`Página ${pagina} de ${total}`, doc.internal.pageSize.getWidth() / 2,
        doc.internal.pageSize.getHeight() - 8, { align: 'center' });
    }
  }

  // O jsPDF define save() dentro de cada instância; isolar a chamada permite testar sem baixar arquivo.
  protected baixar(doc: jsPDF, nomeDoArquivo: string): void {
    doc.save(nomeDoArquivo);
  }

  private dataParaArquivo(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private statusAluguelTexto(status: string): string {
    const textos: { [key: string]: string } = {
      Reservado: 'Reservado', EmAndamento: 'Em andamento', Concluido: 'Concluído', Cancelado: 'Cancelado'
    };
    return textos[status] ?? status;
  }

  private calcularTaxas(aluguel: AluguelCompletoDto): number {
    if (!aluguel.taxasServicos) return 0;
    return aluguel.taxasServicos.reduce((total, taxa) => total + taxa.preco, 0);
  }

  private formatarData(dataString?: string): string {
    if (!dataString) return '';
    const data = new Date(dataString);
    return data.toLocaleDateString('pt-BR');
  }

  private formatarMoeda(valor?: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL'
    }).format(valor ?? 0);
  }

  private getNivelCombustivelText(nivel: string): string {
    const niveis: { [key: string]: string } = {
      'Cheio': 'Cheio',
      'TresQuartos': 'Três Quartos',
      'Metade': 'Metade',
      'UmQuarto': 'Um Quarto',
      'Vazio': 'Vazio'
    };
    return niveis[nivel] || nivel;
  }
}
