import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, ParamMap } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PlanosCobrancaService } from './planos-cobranca.service';
import { GruposVeiculoService } from '../grupoveiculos/grupos-veiculo.service';
import { PlanoCobrancaDto } from './plano-cobranca.model';
import { GrupoVeiculoDto } from '../grupoveiculos/grupo-veiculo.model';
import { Subscription } from 'rxjs';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-plano-cobranca-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './plano-cobranca-form.component.html',
  styleUrls: ['../scss/global.scss']
})
export class PlanoCobrancaFormComponent implements OnInit, OnDestroy {
  form!: FormGroup;
  id?: string | null = null;
  loading = false;
  editMode = false;
  serverErrors: string[] = [];
  grupos: GrupoVeiculoDto[] = [];

  private sub: Subscription | null = null;

  constructor(
    private fb: FormBuilder,
    private svc: PlanosCobrancaService,
    private gruposSvc: GruposVeiculoService,
    private route: ActivatedRoute,
    private router: Router,
    private snack: MatSnackBar
  ) {
    this.form = this.fb.group({
      nome: ['', Validators.required],
      grupoAutomovelId: ['', Validators.required],
      precoDiaria: [0, [Validators.required, Validators.min(0)]],
      precoPorKm: [0, [Validators.min(0)]],
      kmLivreLimite: [0, [Validators.min(0)]]
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
      next: p => {
        this.form.patchValue(p);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snack.open('Erro ao carregar plano de cobrança', 'Fechar', { duration: 4000 });
        this.router.navigate(['/planos-cobranca']);
      }
    });
  }

  salvar() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.snack.open('Verifique os campos obrigatórios', 'Fechar', { duration: 3500 });
      return;
    }

    const payload: PlanoCobrancaDto = this.form.getRawValue();
    this.loading = true;

    const successHandler = () => {
      this.loading = false;
      this.snack.open(this.editMode ? 'Plano de cobrança atualizado' : 'Plano de cobrança criado', 'Fechar', { duration: 3000 });
      this.router.navigate(['/planos-cobranca']);
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
    this.router.navigate(['/planos-cobranca']);
  }
}
