import { Component, OnInit } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DevolucoesService } from './devolucoes.service';
import { DevolucaoCompletoDto, NIVEL_COMBUSTIVEL } from './devolucao.model';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';

@Component({
  selector: 'app-devolucao-view',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './devolucao-view.component.html',
  styleUrls: ['../scss/global.scss']
})
export class DevolucaoViewComponent implements OnInit {
  devolucao?: DevolucaoCompletoDto;
  loading = false;

  constructor(
    private svc: DevolucoesService,
    private route: ActivatedRoute,
    private router: Router,
    private snack: MatSnackBar
  ) {}

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.carregar(id);
    }
  }

  carregar(id: string) {
    this.loading = true;
    this.svc.obter(id).subscribe({
      next: d => {
        this.devolucao = d;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snack.open('Erro ao carregar devolução', 'Fechar', { duration: 4000 });
        this.router.navigate(['/devolucoes']);
      }
    });
  }

  getNivelCombustivelText(nivel: string): string {
    return NIVEL_COMBUSTIVEL[nivel as keyof typeof NIVEL_COMBUSTIVEL] || nivel;
  }

  formatarData(dataString: string): string {
    if (!dataString) return '';
    const data = new Date(dataString);
    return data.toLocaleDateString('pt-BR');
  }

  formatarMoeda(valor: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL'
    }).format(valor);
  }

  excluir() {
    if (!this.devolucao?.id || !confirm('Deseja realmente excluir esta devolução?')) return;

    this.svc.excluir(this.devolucao.id).subscribe({
      next: () => {
        this.snack.open('Devolução excluída com sucesso', 'Fechar', { duration: 3000 });
        this.router.navigate(['/devolucoes']);
      },
      error: (err) => {
        console.error('Erro ao excluir devolução:', err);
        this.snack.open('Falha ao excluir devolução', 'Fechar', { duration: 4000 });
      }
    });
  }

  voltar() {
    this.router.navigate(['/devolucoes']);
  }
}
