import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, ParamMap } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { DesafiosCupomService } from './desafios-cupom.service';
import { DesafioCupomDto } from './desafio-cupom.model';
import { CuponsService } from '../cupons/cupons.service';
import { CupomDto } from '../cupons/cupom.model';

@Component({
  selector: 'app-desafio-cupom-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './desafio-cupom-form.component.html',
  styleUrls: ['../scss/global.scss']
})
export class DesafioCupomFormComponent implements OnInit, OnDestroy {
  form!: FormGroup;
  id?: string | null = null;
  loading = false;
  editMode = false;
  serverErrors: string[] = [];
  cupons: CupomDto[] = [];

  private sub: Subscription | null = null;

  constructor(
    private fb: FormBuilder,
    private svc: DesafiosCupomService,
    private cuponsSvc: CuponsService,
    private route: ActivatedRoute,
    private router: Router,
    private snack: MatSnackBar
  ) {
    this.form = this.fb.group({
      nome: ['', Validators.required],
      descricao: ['', Validators.required],
      metaQuantidadeAlugueis: [1, [Validators.required, Validators.min(1)]],
      periodoDias: [30, [Validators.required, Validators.min(1)]],
      cupomRecompensaId: ['', Validators.required],
      ativo: [true]
    });
  }

  ngOnInit() {
    this.cuponsSvc.listar().subscribe({ next: c => (this.cupons = c) });

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

  carregar(id: string) {
    this.loading = true;
    this.svc.obter(id).subscribe({
      next: d => {
        this.form.patchValue({ ...d, cupomRecompensaId: d.cupomRecompensaId ?? '' });
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snack.open('Erro ao carregar desafio', 'Fechar', { duration: 4000 });
        this.router.navigate(['/desafios-cupom']);
      }
    });
  }

  salvar() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.snack.open('Verifique os campos obrigatórios', 'Fechar', { duration: 3500 });
      return;
    }

    const payload: DesafioCupomDto = this.form.getRawValue();
    this.loading = true;

    const successHandler = () => {
      this.loading = false;
      this.snack.open(this.editMode ? 'Desafio atualizado' : 'Desafio criado', 'Fechar', { duration: 3000 });
      this.router.navigate(['/desafios-cupom']);
    };

    const errorHandler = (err: any) => {
      this.loading = false;
      this.serverErrors = [err?.message ?? 'Erro desconhecido'];
      this.snack.open(this.serverErrors[0], 'Fechar', { duration: 4000 });
    };

    if (this.editMode && this.id) {
      this.svc.atualizar(this.id, payload).subscribe({ next: successHandler, error: errorHandler });
    } else {
      this.svc.criar(payload).subscribe({ next: successHandler, error: errorHandler });
    }
  }

  cancelar() {
    this.router.navigate(['/desafios-cupom']);
  }
}
