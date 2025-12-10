// import { Injectable } from '@angular/core';
// import { jsPDF } from 'jspdf';
// import autoTable from 'jspdf-autotable';
// import { AluguelCompletoDto } from '../alugueis/aluguel.model';
// import { DevolucaoDto } from '../devolucoes/devolucao.model';

// @Injectable({
//   providedIn: 'root'
// })
// export class PdfService {

//   gerarReciboDevolucao(aluguel: AluguelCompletoDto, devolucao: DevolucaoDto): void {
//     const doc = new jsPDF();
//     const dataAtual = new Date().toLocaleDateString('pt-BR');
//     const horaAtual = new Date().toLocaleTimeString('pt-BR');

//     // Cabeçalho
//     doc.setFontSize(20);
//     doc.text('RECIBO DE DEVOLUÇÃO', 105, 20, { align: 'center' });

//     doc.setFontSize(12);
//     doc.text('Locadora de Veículos', 105, 30, { align: 'center' });
//     doc.text(`Data: ${dataAtual} ${horaAtual}`, 105, 40, { align: 'center' });
//     doc.text('----------------------------------------------------', 105, 45, { align: 'center' });

//     // Informações do Aluguel
//     doc.setFontSize(14);
//     doc.text('Informações do Aluguel', 20, 60);

//     doc.setFontSize(10);
//     let y = 70;

//     doc.text(`ID do Aluguel: ${aluguel.id}`, 20, y);
//     y += 8;
//     doc.text(`Condutor: ${aluguel.condutorNome}`, 20, y);
//     y += 8;
//     doc.text(`CPF: ${aluguel.condutorNome ? this.formatarCPF(aluguel.condutorNome) : 'N/A'}`, 20, y);
//     y += 8;
//     doc.text(`Veículo: ${aluguel.automovelPlaca}`, 20, y);
//     y += 8;
//     doc.text(`Cliente: ${aluguel.clienteNome}`, 20, y);
//     y += 8;
//     doc.text(`Data Saída: ${this.formatarData(aluguel.dataSaida)}`, 20, y);
//     y += 8;
//     doc.text(`Data Retorno Previsto: ${this.formatarData(aluguel.dataRetornoPrevisto)}`, 20, y);
//     y += 8;
//     doc.text(`Valor Previsto: ${this.formatarMoeda(aluguel.valorPrevisto)}`, 20, y);

//     y += 12;

//     // Informações da Devolução
//     doc.setFontSize(14);
//     doc.text('Informações da Devolução', 20, y);

//     doc.setFontSize(10);
//     y += 10;
//     doc.text(`Data Devolução: ${this.formatarData(devolucao.dataDevolucao)}`, 20, y);
//     y += 8;
//     doc.text(`Quilometragem Final: ${devolucao.quilometragemFinal} km`, 20, y);
//     y += 8;
//     doc.text(`Combustível no Tanque: ${devolucao.combustivelNoTanque} L`, 20, y);
//     y += 8;
//     doc.text(`Nível Combustível: ${this.getNivelCombustivelText(devolucao.nivelCombustivel)}`, 20, y);

//     y += 12;

//     // Detalhes Financeiros
//     doc.setFontSize(14);
//     doc.text('Detalhes Financeiros', 20, y);

//     // Tabela de valores
//     const tableData = [
//       ['Valor do Aluguel', this.formatarMoeda(aluguel.valorPrevisto)],
//       ['Taxas e Serviços', this.formatarMoeda(this.calcularTaxas(aluguel))],
//       ['Multas', this.formatarMoeda(devolucao.valorMultas)],
//       ['Adicional Combustível', this.formatarMoeda(devolucao.valorAdicionalCombustivel)],
//       ['Valor da Caução', 'R$ 1.000,00'],
//       ['', ''],
//       ['TOTAL', this.formatarMoeda(devolucao.valorTotal + 1000)] // + caução
//     ];

//     autoTable(doc, {
//       startY: y + 10,
//       head: [['Descrição', 'Valor']],
//       body: tableData,
//       theme: 'grid',
//       headStyles: { fillColor: [41, 128, 185], textColor: 255 },
//       margin: { left: 20, right: 20 }
//     });

//     // Rodapé
//     const finalY = (doc as any).lastAutoTable.finalY + 20;
//     doc.text('----------------------------------------------------', 105, finalY, { align: 'center' });
//     doc.text('Assinatura do Funcionário', 105, finalY + 10, { align: 'center' });
//     doc.text('_________________________', 105, finalY + 25, { align: 'center' });
//     doc.text('Assinatura do Cliente', 105, finalY + 40, { align: 'center' });
//     doc.text('_________________________', 105, finalY + 55, { align: 'center' });

//     // Salvar PDF
//     doc.save(`recibo-devolucao-${aluguel.id}-${dataAtual.replace(/\//g, '-')}.pdf`);
//   }

//   private calcularTaxas(aluguel: AluguelCompletoDto): number {
//     if (!aluguel.taxasServicos) return 0;
//     return aluguel.taxasServicos.reduce((total, taxa) => total + taxa.preco, 0);
//   }

//   private formatarData(dataString: string): string {
//     if (!dataString) return '';
//     const data = new Date(dataString);
//     return data.toLocaleDateString('pt-BR');
//   }

//   private formatarMoeda(valor: number): string {
//     return new Intl.NumberFormat('pt-BR', {
//       style: 'currency',
//       currency: 'BRL'
//     }).format(valor);
//   }

//   private formatarCPF(cpf: string): string {
//     if (!cpf) return '';
//     cpf = cpf.replace(/\D/g, '');
//     return cpf.replace(/(\d{3})(\d{3})(\d{3})(\d{2})/, '$1.$2.$3-$4');
//   }

//   private getNivelCombustivelText(nivel: string): string {
//     const niveis: { [key: string]: string } = {
//       'Cheio': 'Cheio',
//       'TresQuartos': 'Três Quartos',
//       'Metade': 'Metade',
//       'UmQuarto': 'Um Quarto',
//       'Vazio': 'Vazio'
//     };
//     return niveis[nivel] || nivel;
//   }
// }
