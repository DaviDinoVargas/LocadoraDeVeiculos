import { Injectable } from '@angular/core';
import { jsPDF } from 'jspdf';
import autoTable from 'jspdf-autotable';
import { mascararPlaca } from '../../shared/mascaras/mascara.directive';
import { AluguelCompletoDto, SelecionarAlugueisDto } from '../alugueis/aluguel.model';
import { DevolucaoCompletoDto, SelecionarDevolucoesDto } from '../devolucoes/devolucao.model';
import {
  COR, MARGEM, TomSelo, cabecalho, cartaoDeDados, cartoesResumo, estiloDeTabela, rodapeDePaginas, tituloDeSecao
} from './pdf-tema';

/** Mesmos tons de selo da tela de aluguéis (Reservado = info, Em andamento = alerta, Concluído = sucesso, Cancelado = erro). */
const TOM_DO_STATUS: Record<string, TomSelo> = {
  Reservado: 'info', 'Em andamento': 'warning', 'Concluído': 'success', Cancelado: 'error'
};

@Injectable({
  providedIn: 'root'
})
export class PdfService {

  /** Monta o recibo; separado de {@link gerarReciboDevolucao} para poder ser conferido sem baixar arquivo. */
  montarReciboDevolucao(devolucao: DevolucaoCompletoDto, aluguel: AluguelCompletoDto): jsPDF {
    const doc = new jsPDF();
    const largura = doc.internal.pageSize.getWidth();
    const altura = doc.internal.pageSize.getHeight();

    let y = cabecalho(doc, 'Recibo de devolução', `Devolução realizada em ${this.formatarData(devolucao.dataDevolucao)}`);

    y = cartaoDeDados(doc, y, 'Aluguel', [
      ['Condutor', devolucao.condutorNome ?? ''],
      ['Veículo', mascararPlaca(devolucao.automovelPlaca)],
      ['Cliente', devolucao.clienteNome ?? ''],
      ['Saída', this.formatarData(aluguel.dataSaida)],
      ['Retorno previsto', this.formatarData(aluguel.dataRetornoPrevisto)],
      ['Valor previsto', this.formatarMoeda(aluguel.valorPrevisto)]
    ]);

    y = cartaoDeDados(doc, y, 'Devolução', [
      ['Data da devolução', this.formatarData(devolucao.dataDevolucao)],
      ['Quilometragem final', `${devolucao.quilometragemFinal} km`],
      ['Combustível no tanque', `${devolucao.combustivelNoTanque} L`],
      ['Nível do combustível', this.getNivelCombustivelText(devolucao.nivelCombustivel)]
    ]);

    y = tituloDeSecao(doc, y + 2, 'Detalhes financeiros');
    autoTable(doc, {
      ...estiloDeTabela({ direita: [1] }),
      startY: y + 2,
      head: [['Descrição', 'Valor']],
      body: [
        ['Valor do aluguel', this.formatarMoeda(aluguel.valorPrevisto)],
        ['Taxas e serviços', this.formatarMoeda(this.calcularTaxas(aluguel))],
        ['Multas', this.formatarMoeda(devolucao.valorMultas)],
        ['Adicional de combustível', this.formatarMoeda(devolucao.valorAdicionalCombustivel)],
        ['Valor da caução', this.formatarMoeda(aluguel.valorCaucao)]
      ],
      foot: [['Total', this.formatarMoeda(devolucao.valorTotal + (aluguel.valorCaucao ?? 0))]]
    });

    // Assinaturas lado a lado, sempre na mesma página do total (abre outra página só se faltar espaço)
    let yAssinatura = (doc as jsPDF & { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 30;
    if (yAssinatura > altura - 30) {
      doc.addPage();
      yAssinatura = 50;
    }
    const metade = (largura - 2 * MARGEM - 16) / 2;
    [['Assinatura do funcionário', MARGEM], ['Assinatura do cliente', MARGEM + metade + 16]].forEach(([rotulo, x]) => {
      doc.setDrawColor(...COR.contorno);
      doc.setLineWidth(0.3);
      doc.line(x as number, yAssinatura, (x as number) + metade, yAssinatura);
      doc.setFont('helvetica', 'normal');
      doc.setFontSize(8.5);
      doc.setTextColor(...COR.textoSecundario);
      doc.text(rotulo as string, x as number, yAssinatura + 5);
    });

    doc.setFontSize(7.5);
    doc.text(`Código da devolução: ${devolucao.id}`, MARGEM, altura - 17);

    rodapeDePaginas(doc);
    return doc;
  }

  gerarReciboDevolucao(devolucao: DevolucaoCompletoDto, aluguel: AluguelCompletoDto): void {
    const dataAtual = new Date().toLocaleDateString('pt-BR');
    this.baixar(this.montarReciboDevolucao(devolucao, aluguel), `recibo-devolucao-${devolucao.id}-${dataAtual.replace(/\//g, '-')}.pdf`);
  }

  // ---- Relatórios em lista (exportam exatamente o que está na tela, já filtrado) ----------------

  /** Monta o relatório de aluguéis; separado de {@link gerarRelatorioAlugueis} para poder ser conferido sem baixar arquivo. */
  montarRelatorioAlugueis(alugueis: SelecionarAlugueisDto[], filtro: string): jsPDF {
    const doc = new jsPDF({ orientation: 'landscape' });
    let y = cabecalho(doc, 'Relatório de aluguéis', `Filtro: ${filtro}`);

    const total = alugueis.reduce((s, a) => s + (a.valorPrevisto ?? 0), 0);
    y = cartoesResumo(doc, y, [
      { rotulo: 'Aluguéis na lista', valor: String(alugueis.length) },
      { rotulo: 'Valor previsto total', valor: this.formatarMoeda(total) },
      { rotulo: 'Em andamento', valor: String(alugueis.filter(a => a.status === 'EmAndamento').length) }
    ]);

    autoTable(doc, {
      ...estiloDeTabela({ selo: { coluna: 6, tom: texto => TOM_DO_STATUS[texto] ?? 'neutral' }, direita: [5] }),
      startY: y,
      head: [['Condutor', 'Veículo', 'Cliente', 'Saída', 'Retorno previsto', 'Valor previsto', 'Status']],
      body: alugueis.map(a => [
        a.condutorNome, mascararPlaca(a.automovelPlaca), a.clienteNome,
        this.formatarData(a.dataSaida), this.formatarData(a.dataRetornoPrevisto),
        this.formatarMoeda(a.valorPrevisto), this.statusAluguelTexto(a.status)
      ]),
      foot: [[
        { content: `${alugueis.length} aluguel(éis)`, colSpan: 5 },
        this.formatarMoeda(total),
        ''
      ]]
    });

    rodapeDePaginas(doc);
    return doc;
  }

  gerarRelatorioAlugueis(alugueis: SelecionarAlugueisDto[], filtro: string): void {
    this.baixar(this.montarRelatorioAlugueis(alugueis, filtro), `relatorio-alugueis-${this.dataParaArquivo()}.pdf`);
  }

  montarRelatorioDevolucoes(devolucoes: SelecionarDevolucoesDto[]): jsPDF {
    const doc = new jsPDF({ orientation: 'landscape' });
    let y = cabecalho(doc, 'Relatório de devoluções', 'Todas as devoluções registradas');

    const total = devolucoes.reduce((s, d) => s + (d.valorTotal ?? 0), 0);
    y = cartoesResumo(doc, y, [
      { rotulo: 'Devoluções na lista', valor: String(devolucoes.length) },
      { rotulo: 'Valor total', valor: this.formatarMoeda(total) },
      { rotulo: 'Valor médio por devolução', valor: this.formatarMoeda(devolucoes.length ? total / devolucoes.length : 0) }
    ]);

    autoTable(doc, {
      ...estiloDeTabela({ direita: [4, 5] }),
      startY: y,
      head: [['Condutor', 'Veículo', 'Cliente', 'Data da devolução', 'Km final', 'Valor total']],
      body: devolucoes.map(d => [
        d.condutorNome, mascararPlaca(d.automovelPlaca), d.clienteNome,
        this.formatarData(d.dataDevolucao), `${d.quilometragemFinal} km`, this.formatarMoeda(d.valorTotal)
      ]),
      foot: [[
        { content: `${devolucoes.length} devolução(ões)`, colSpan: 5 },
        this.formatarMoeda(total)
      ]]
    });

    rodapeDePaginas(doc);
    return doc;
  }

  gerarRelatorioDevolucoes(devolucoes: SelecionarDevolucoesDto[]): void {
    this.baixar(this.montarRelatorioDevolucoes(devolucoes), `relatorio-devolucoes-${this.dataParaArquivo()}.pdf`);
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
      'TresQuartos': 'Três quartos',
      'Metade': 'Metade',
      'UmQuarto': 'Um quarto',
      'Vazio': 'Vazio'
    };
    return niveis[nivel] || nivel;
  }
}
