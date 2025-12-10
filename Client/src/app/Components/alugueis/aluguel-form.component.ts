import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, ParamMap } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AlugueisService } from './alugueis.service';
import { ClientesService } from '../clientes/clientes.service';
import { CondutoresService } from '../condutor/condutores.service';
import { VeiculosService } from '../veiculos/veiculos.service';
import { TaxasServicosService } from '../taxa-servico/taxas-servicos.service';
import { AluguelDto, AluguelCompletoDto } from './aluguel.model';
import { ClienteDto } from '../clientes/cliente.model';
import { CondutorDto } from '../condutor/condutor.model';
import { VeiculoDto } from '../veiculos/veiculo.model';
import { TaxaServicoDto } from '../taxa-servico/taxa-servico.model';
import { Subscription } from 'rxjs';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-aluguel-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './aluguel-form.component.html',
  styleUrls: ['../scss/global.scss']
})
export class AluguelFormComponent implements OnInit, OnDestroy {
  form!: FormGroup;
  id?: string | null = null;
  loading = false;
  editMode = false;
  serverErrors: string[] = [];

  clientes: ClienteDto[] = [];
  condutores: CondutorDto[] = [];
  condutoresFiltrados: CondutorDto[] = [];
  veiculos: VeiculoDto[] = [];
  veiculosDisponiveis: VeiculoDto[] = [];
  taxasServicos: TaxaServicoDto[] = [];
  taxasSelecionadas: string[] = [];

  private sub: Subscription | null = null;

  constructor(
    private fb: FormBuilder,
    private svc: AlugueisService,
    private clientesSvc: ClientesService,
    private condutoresSvc: CondutoresService,
    private veiculosSvc: VeiculosService,
    private taxasSvc: TaxasServicosService,
    private route: ActivatedRoute,
    private router: Router,
    private snack: MatSnackBar
  ) {
    this.form = this.fb.group({
      clienteId: ['', Validators.required],
      condutorId: ['', Validators.required],
      automovelId: ['', Validators.required],
      dataSaida: ['', Validators.required],
      dataRetornoPrevisto: ['', Validators.required],
      valorPrevisto: [0, [Validators.required, Validators.min(0)]],
      taxasServicosIds: [[]]
    });
  }

  ngOnInit() {
    this.carregarDados();

    this.sub = this.route.paramMap.subscribe((params: ParamMap) => {
      this.id = params.get('id');
      this.editMode = !!this.id;

      if (this.editMode && this.id) {
        this.carregar(this.id);
      }
    });
  }

  ngOnDestroy() {
    this.sub?.unsubscribe();
  }

  carregarDados() {
    this.carregarClientes();
    this.carregarCondutores();
    this.carregarVeiculos();
    this.carregarTaxasServicos();
  }

  carregarClientes() {
    this.clientesSvc.listar().subscribe({
      next: clientes => this.clientes = clientes,
      error: () => this.snack.open('Erro ao carregar clientes', 'Fechar', { duration: 4000 })
    });
  }

  carregarCondutores() {
    this.condutoresSvc.listar().subscribe({
      next: condutores => {
        this.condutores = condutores;
        this.condutoresFiltrados = [...condutores];
      },
      error: () => this.snack.open('Erro ao carregar condutores', 'Fechar', { duration: 4000 })
    });
  }

  carregarVeiculos() {
    this.veiculosSvc.listar().subscribe({
      next: veiculos => {
        this.veiculos = veiculos;
        this.veiculosDisponiveis = veiculos; // TODO: Filtrar veículos disponíveis
      },
      error: () => this.snack.open('Erro ao carregar veículos', 'Fechar', { duration: 4000 })
    });
  }

  carregarTaxasServicos() {
    this.taxasSvc.listar().subscribe({
      next: taxas => this.taxasServicos = taxas,
      error: () => this.snack.open('Erro ao carregar taxas e serviços', 'Fechar', { duration: 4000 })
    });
  }

  onClienteChange(event: any) {
    const clienteId = event.target.value;
    if (clienteId) {
      // Filtrar condutores por cliente selecionado
      this.condutoresFiltrados = this.condutores.filter(c => c.clienteId === clienteId);

      // Resetar condutor selecionado
      this.form.patchValue({ condutorId: '' });
    } else {
      this.condutoresFiltrados = [...this.condutores];
    }
  }

  onTaxaChange(event: any, taxa: TaxaServicoDto) {
    if (event.target.checked) {
      this.taxasSelecionadas.push(taxa.id!);
    } else {
      const index = this.taxasSelecionadas.indexOf(taxa.id!);
      if (index > -1) {
        this.taxasSelecionadas.splice(index, 1);
      }
    }
    this.form.patchValue({ taxasServicosIds: this.taxasSelecionadas });
  }

  calcularValorTotal(): number {
    const valorPrevisto = this.form.get('valorPrevisto')?.value || 0;
    const taxasSelecionadas = this.taxasServicos.filter(t => this.taxasSelecionadas.includes(t.id!));
    const valorTaxas = taxasSelecionadas.reduce((total, taxa) => total + taxa.preco, 0);
    return valorPrevisto + valorTaxas + 1000; // + caução de R$ 1000
  }

  formatarMoeda(valor: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL'
    }).format(valor);
  }

  carregar(id: string) {
    this.loading = true;
    this.svc.obter(id).subscribe({
      next: (a: any) => {
        this.form.patchValue(a);
        this.taxasSelecionadas = a.taxasServicos?.map((t: any) => t.id) || [];
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snack.open('Erro ao carregar aluguel', 'Fechar', { duration: 4000 });
        this.router.navigate(['/alugueis']);
      }
    });
  }

  salvar() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.snack.open('Verifique os campos obrigatórios', 'Fechar', { duration: 3500 });
      return;
    }

    const payload: AluguelDto = {
      ...this.form.getRawValue(),
      valorCaucao: 1000, // Valor fixo de caução
      status: 'Reservado'
    };

    this.loading = true;

    const successHandler = () => {
      this.loading = false;
      this.snack.open(this.editMode ? 'Aluguel atualizado' : 'Aluguel criado', 'Fechar', { duration: 3000 });
      this.router.navigate(['/alugueis']);
    };

    const errorHandler = (err: any) => {
      this.loading = false;
      this.serverErrors = [err?.message ?? 'Erro desconhecido'];
      this.snack.open(this.serverErrors[0], 'Fechar', { duration: 4000 });
    };

    if (this.editMode && this.id) {
      this.svc.atualizar(this.id, payload).subscribe({
        next: successHandler,
        error: errorHandler
      });
    } else {
      this.svc.criar(payload).subscribe({
        next: successHandler,
        error: errorHandler
      });
    }
  }

  cancelar() {
    this.router.navigate(['/alugueis']);
  }
}
