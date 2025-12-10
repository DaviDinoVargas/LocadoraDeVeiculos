import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, ParamMap } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { VeiculosService } from './veiculos.service';
import { GruposVeiculoService } from '../grupoveiculos/grupos-veiculo.service';
import { VeiculoDto } from './veiculo.model';
import { GrupoVeiculoDto } from '../grupoveiculos/grupo-veiculo.model';
import { Subscription } from 'rxjs';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-veiculo-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './veiculo-form.component.html',
  styleUrls: ['../scss/global.scss']
})
export class VeiculoFormComponent implements OnInit, OnDestroy {
  form!: FormGroup;
  id?: string | null = null;
  loading = false;
  editMode = false;
  serverErrors: string[] = [];
  grupos: GrupoVeiculoDto[] = [];
  anoAtual = new Date().getFullYear();

  private sub: Subscription | null = null;

  constructor(
    private fb: FormBuilder,
    private svc: VeiculosService,
    private gruposSvc: GruposVeiculoService,
    private route: ActivatedRoute,
    private router: Router,
    private snack: MatSnackBar
  ) {
    this.form = this.fb.group({
      placa: ['', [Validators.required, Validators.pattern(/^[A-Z]{3}-[0-9][A-Z0-9][0-9]{2}$/)]],
      marca: ['', Validators.required],
      modelo: ['', Validators.required],
      cor: ['', Validators.required],
      ano: [this.anoAtual, [Validators.required, Validators.min(1900), Validators.max(this.anoAtual + 1)]],
      tipoCombustivel: ['', Validators.required],
      capacidadeTanque: [0, [Validators.required, Validators.min(0)]],
      grupoAutomovelId: ['', Validators.required],
      foto: ['']
    });
  }

  ngOnInit() {
    this.carregarGrupos();

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

  carregarGrupos() {
    this.gruposSvc.listar().subscribe({
      next: grupos => this.grupos = grupos,
      error: () => this.snack.open('Erro ao carregar grupos', 'Fechar', { duration: 4000 })
    });
  }

  carregar(id: string) {
    this.loading = true;
    this.svc.obter(id).subscribe({
      next: v => {
        this.form.patchValue(v);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snack.open('Erro ao carregar veículo', 'Fechar', { duration: 4000 });
        this.router.navigate(['/veiculos']);
      }
    });
  }

  salvar() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.snack.open('Verifique os campos obrigatórios', 'Fechar', { duration: 3500 });
      return;
    }

    const payload: VeiculoDto = this.form.getRawValue();
    this.loading = true;

    const successHandler = () => {
      this.loading = false;
      this.snack.open(this.editMode ? 'Veículo atualizado' : 'Veículo criado', 'Fechar', { duration: 3000 });
      this.router.navigate(['/veiculos']);
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
    this.router.navigate(['/veiculos']);
  }
}
