import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AlugueisService } from './alugueis.service';
import { AluguelCompletoDto } from './aluguel.model';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-aluguel-devolucao-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './aluguel-devolucao-form.component.html',
  styleUrls: ['../scss/global.scss']
})
export class AluguelDevolucaoFormComponent implements OnInit {
  form!: FormGroup;
  id?: string | null = null;
  loading = false;
  serverErrors: string[] = [];
  aluguel?: AluguelCompletoDto;
  multa: number = 0;
  valorTaxas: number = 0;

  constructor(
    private fb: FormBuilder,
    private svc: AlugueisService,
    private route: ActivatedRoute,
    private router: Router,
    private snack: MatSnackBar
  ) {
    this.form = this.fb.group({
      dataDevolucao: ['', Validators.required],
      quilometragemRodada: [0, [Validators.required, Validators.min(0)]],
      nivelCombustivel: [100, [Validators.required, Validators.min(0), Validators.max(100)]]
    });
  }

  ngOnInit() {
    this.id = this.route.snapshot.paramMap.get('id');
    if (this.id) {
      this.carregarAluguel(this.id);
    }
  }

  carregarAluguel(id: string) {
    this.loading = true;
    this.svc.obter(id).subscribe({
      next: (a: any) => {
        this.aluguel = a;
        this.valorTaxas = a.taxasServicos?.reduce((total: number, taxa: any) => total + taxa.preco, 0) || 0;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snack.open('Erro ao carregar aluguel', 'Fechar', { duration: 4000 });
        this.router.navigate(['/alugueis']);
      }
    });
  }

  calcularMulta() {
    if (!this.aluguel || !this.form.get('dataDevolucao')?.value) return 0;

    const dataRetornoPrevisto = new Date(this.aluguel.dataRetornoPrevisto);
    const dataDevolucao = new Date(this.form.get('dataDevolucao')?.value);

    if (dataDevolucao > dataRetornoPrevisto) {
      const diasAtraso = Math.ceil((dataDevolucao.getTime() - dataRetornoPrevisto.getTime()) / (1000 * 60 * 60 * 24));
      return this.aluguel.valorPrevisto * 0.1; // 10% de multa
    }

    return 0;
  }

  calcularValorTotal(): number {
    if (!this.aluguel) return 0;

    this.multa = this.calcularMulta();
    const multaCombustivel = this.calcularMultaCombustivel();

    return this.aluguel.valorPrevisto + this.valorTaxas + this.multa + multaCombustivel;
  }

  calcularMultaCombustivel(): number {
    const nivelCombustivel = this.form.get('nivelCombustivel')?.value || 100;
    if (nivelCombustivel < 100) {
      // Calcular custo do combustível faltante
      // Isso dependeria do preço do combustível e capacidade do tanque
      return 0; // Implementar cálculo real
    }
    return 0;
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

  salvar() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.snack.open('Verifique os campos obrigatórios', 'Fechar', { duration: 3500 });
      return;
    }

    if (!this.id) return;

    this.loading = true;
    // Aqui você faria a chamada para a API de devolução
    // Por enquanto, apenas redirecionamos
    setTimeout(() => {
      this.loading = false;
      this.snack.open('Devolução registrada com sucesso', 'Fechar', { duration: 3000 });
      this.router.navigate(['/alugueis']);
    }, 1000);
  }

  cancelar() {
    this.router.navigate(['/alugueis']);
  }
}
