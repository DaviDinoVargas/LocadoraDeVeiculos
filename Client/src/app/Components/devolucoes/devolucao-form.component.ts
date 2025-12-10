import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DevolucoesService } from './devolucoes.service';
import { AlugueisService } from '../alugueis/alugueis.service';
import { ConfiguracoesService } from '../configuracoes/configuracoes.service';
import { DevolucaoDto, NIVEL_COMBUSTIVEL_OPTIONS } from './devolucao.model';
import { AluguelCompletoDto, SelecionarAlugueisDto, TaxaServicoAluguelDto } from '../alugueis/aluguel.model';
import { ConfiguracaoDto } from '../configuracoes/configuracao.model';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-devolucao-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './devolucao-form.component.html',
  styleUrls: ['../scss/global.scss']
})
export class DevolucaoFormComponent implements OnInit {
  form!: FormGroup;
  aluguelId?: string | null = null;
  loading = false;
  loadingAluguel = false;
  serverErrors: string[] = [];
  aluguel?: AluguelCompletoDto;
  configuracao?: ConfiguracaoDto;

  multa: number = 0;
  valorTaxas: number = 0;
  valorCombustivel: number = 0;
  dataAtual: string;
  nivelCombustivelOptions = NIVEL_COMBUSTIVEL_OPTIONS;

  constructor(
    private fb: FormBuilder,
    private svc: DevolucoesService,
    private aluguelSvc: AlugueisService,
    private configSvc: ConfiguracoesService,
    private route: ActivatedRoute,
    private router: Router,
    private snack: MatSnackBar
  ) {
    this.form = this.fb.group({
      dataDevolucao: ['', [Validators.required]],
      quilometragemFinal: [0, [Validators.required, Validators.min(0)]],
      nivelCombustivel: ['', Validators.required],
      combustivelNoTanque: [0, [Validators.required, Validators.min(0)]]
    });

    const hoje = new Date();
    this.dataAtual = hoje.toISOString().split('T')[0];
  }

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      const placa = params['placa'];
      if (placa) {
        this.buscarAluguelPorPlaca(placa);
      }
    });

    this.aluguelId = this.route.snapshot.paramMap.get('id');
    if (this.aluguelId) {
      this.carregarAluguel(this.aluguelId);
      this.carregarConfiguracao();
    }
  }

  buscarAluguelPorPlaca(placa: string) {
    this.aluguelSvc.selecionarEmAberto().subscribe({
      next: (alugueis: SelecionarAlugueisDto[]) => {
        const aluguel = alugueis.find(a =>
          a.automovelPlaca && a.automovelPlaca.toUpperCase() === placa.toUpperCase()
        );
        if (aluguel) {
          this.aluguelId = aluguel.id;
          this.carregarAluguel(aluguel.id);
        } else {
          this.snack.open(`Nenhum aluguel em aberto encontrado para a placa ${placa}`, 'Fechar', { duration: 4000 });
        }
      },
      error: () => {
        this.snack.open('Erro ao buscar aluguel pela placa', 'Fechar', { duration: 4000 });
      }
    });
  }

  carregarAluguel(id: string) {
    this.loadingAluguel = true;
    this.aluguelSvc.obter(id).subscribe({
      next: (a: AluguelCompletoDto) => {
        this.aluguel = a;
        this.valorTaxas = a.taxasServicos?.reduce((total: number, taxa: TaxaServicoAluguelDto) => total + taxa.preco, 0) || 0;
        this.loadingAluguel = false;
      },
      error: () => {
        this.loadingAluguel = false;
        this.snack.open('Erro ao carregar aluguel', 'Fechar', { duration: 4000 });
        this.router.navigate(['/alugueis']);
      }
    });
  }

  carregarConfiguracao() {
    this.configSvc.obter().subscribe({
      next: (config: ConfiguracaoDto) => this.configuracao = config,
      error: () => this.snack.open('Erro ao carregar configurações', 'Fechar', { duration: 4000 })
    });
  }

  calcularMulta(): number {
    if (!this.aluguel || !this.form.get('dataDevolucao')?.value) return 0;

    const dataRetornoPrevisto = new Date(this.aluguel.dataRetornoPrevisto);
    const dataDevolucao = new Date(this.form.get('dataDevolucao')?.value);

    if (dataDevolucao > dataRetornoPrevisto) {
      return this.aluguel.valorPrevisto * 0.1;
    }

    return 0;
  }

  calcularValorCombustivel(): number {
    if (!this.aluguel || !this.configuracao || !this.form.get('combustivelNoTanque')?.value) return 0;

    const nivel = this.form.get('nivelCombustivel')?.value;
    let percentualEsperado = 100;

    switch (nivel) {
      case 'Cheio': percentualEsperado = 100; break;
      case 'TresQuartos': percentualEsperado = 75; break;
      case 'Metade': percentualEsperado = 50; break;
      case 'UmQuarto': percentualEsperado = 25; break;
      case 'Vazio': percentualEsperado = 0; break;
    }

    const combustivelAtual = this.form.get('combustivelNoTanque')?.value;
    const combustivelEsperado = 50 * (percentualEsperado / 100);

    if (combustivelAtual < combustivelEsperado) {
      const litrosFaltantes = combustivelEsperado - combustivelAtual;
      return litrosFaltantes * (this.configuracao?.precoCombustivel || 5.00);
    }

    return 0;
  }

  calcularValorTotal(): number {
    if (!this.aluguel) return 0;

    this.multa = this.calcularMulta();
    this.valorCombustivel = this.calcularValorCombustivel();

    return this.aluguel.valorPrevisto + this.valorTaxas + this.multa + this.valorCombustivel;
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

    if (!this.aluguelId) return;

    const payload: DevolucaoDto = {
      aluguelId: this.aluguelId,
      dataDevolucao: this.form.get('dataDevolucao')?.value,
      quilometragemFinal: this.form.get('quilometragemFinal')?.value,
      nivelCombustivel: this.form.get('nivelCombustivel')?.value,
      combustivelNoTanque: this.form.get('combustivelNoTanque')?.value,
      valorMultas: this.multa,
      valorAdicionalCombustivel: this.valorCombustivel,
      valorTotal: this.calcularValorTotal()
    };

    this.loading = true;
    this.serverErrors = [];

    this.svc.registrar(payload).subscribe({
      next: () => {
        this.loading = false;
        this.snack.open('Devolução registrada com sucesso', 'Fechar', { duration: 3000 });
        this.router.navigate(['/devolucoes']);
      },
      error: (err: any) => {
        this.loading = false;
        this.serverErrors = [err?.message ?? 'Erro desconhecido'];
        this.snack.open(this.serverErrors[0], 'Fechar', { duration: 4000 });
      }
    });
  }

  cancelar() {
    this.router.navigate(['/alugueis']);
  }
}
