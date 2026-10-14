import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';
import { filter } from 'rxjs/operators';
import { ConfirmacaoService } from '../../core/confirmacao.service';
import { NotificacaoService } from '../../core/notificacao.service';
import { formatarData, formatarMoeda } from '../../shared/data-table/data-table.util';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { AluguelCompletoDto } from '../alugueis/aluguel.model';
import { AlugueisService } from '../alugueis/alugueis.service';
import { PdfService } from '../pdf/pdf.service';
import { DevolucaoCompletoDto, NIVEL_COMBUSTIVEL } from './devolucao.model';
import { DevolucoesService } from './devolucoes.service';
import { PlacaPipe } from '../../shared/mascaras/placa.pipe';

@Component({
  selector: 'app-devolucao-view',
  imports: [NgIcon, HlmButtonImports, HlmSkeleton, PageComponent, PageHeaderComponent, PlacaPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './devolucao-view.component.html'
})
export class DevolucaoViewComponent implements OnInit {
  private readonly svc = inject(DevolucoesService);
  private readonly aluguelSvc = inject(AlugueisService);
  private readonly pdfSvc = inject(PdfService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly confirmacao = inject(ConfirmacaoService);
  private readonly notificacao = inject(NotificacaoService);

  readonly devolucao = signal<DevolucaoCompletoDto | undefined>(undefined);
  readonly aluguel = signal<AluguelCompletoDto | undefined>(undefined);
  readonly carregando = signal(false);
  readonly gerandoPdf = signal(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.carregar(id);
    }
  }

  carregar(id: string): void {
    this.carregando.set(true);
    this.svc.obter(id).subscribe({
      next: d => {
        this.devolucao.set(d);
        this.carregando.set(false);
        this.aluguelSvc.obter(d.aluguelId).subscribe({
          next: a => this.aluguel.set(a),
          error: () => undefined // o recibo ainda funciona sem os dados extras do aluguel
        });
      },
      error: () => {
        this.carregando.set(false);
        void this.router.navigate(['/devolucoes']);
      }
    });
  }

  baixarRecibo(): void {
    const devolucao = this.devolucao();
    const aluguel = this.aluguel();
    if (!devolucao || !aluguel) return;
    this.gerandoPdf.set(true);
    try {
      this.pdfSvc.gerarReciboDevolucao(devolucao, aluguel);
    } finally {
      this.gerandoPdf.set(false);
    }
  }

  getNivelCombustivelText(nivel: string): string {
    return NIVEL_COMBUSTIVEL[nivel as keyof typeof NIVEL_COMBUSTIVEL] || nivel;
  }

  formatarData(dataString?: string): string {
    return dataString ? formatarData(dataString) : '—';
  }

  formatarMoeda(valor?: number): string {
    return formatarMoeda(valor ?? 0);
  }

  excluir(): void {
    const id = this.devolucao()?.id;
    if (!id) return;

    this.confirmacao
      .confirmar({ titulo: 'Confirmar exclusão', mensagem: 'Deseja realmente excluir esta devolução?', confirmar: 'Excluir devolução', destrutivo: true })
      .pipe(filter(Boolean))
      .subscribe(() => {
        this.svc.excluir(id).subscribe({
          next: () => {
            this.notificacao.sucesso('Devolução excluída com sucesso');
            void this.router.navigate(['/devolucoes']);
          }
        });
      });
  }
}
