import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, ParamMap } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { CuponsService } from './cupons.service';
import { CupomDto, TIPOS_DESCONTO } from './cupom.model';
import { ParceirosService } from '../parceiros/parceiros.service';
import { ParceiroDto } from '../parceiros/parceiro.model';

@Component({
  selector: 'app-cupom-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './cupom-form.component.html',
  styleUrls: ['../scss/global.scss']
})
export class CupomFormComponent implements OnInit, OnDestroy {
  form!: FormGroup;
  id?: string | null = null;
  loading = false;
  editMode = false;
  serverErrors: string[] = [];
  tiposDesconto = TIPOS_DESCONTO;
  parceiros: ParceiroDto[] = [];

  private sub: Subscription | null = null;

  constructor(
    private fb: FormBuilder,
    private svc: CuponsService,
    private parceirosSvc: ParceirosService,
    private route: ActivatedRoute,
    private router: Router,
    private snack: MatSnackBar
  ) {
    this.form = this.fb.group({
      codigo: ['', Validators.required],
      descricao: ['', Validators.required],
      tipoDesconto: ['', Validators.required],
      valorDesconto: [0, [Validators.required, Validators.min(0)]],
      validoAte: ['', Validators.required],
      limiteUsos: [1, [Validators.required, Validators.min(1)]],
      parceiroId: [''],
      ativo: [true]
    });
  }

  ngOnInit() {
    this.parceirosSvc.listar().subscribe({ next: p => (this.parceiros = p) });

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
      next: c => {
        this.form.patchValue({
          ...c,
          validoAte: c.validoAte ? c.validoAte.substring(0, 10) : '',
          parceiroId: c.parceiroId ?? ''
        });
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snack.open('Erro ao carregar cupom', 'Fechar', { duration: 4000 });
        this.router.navigate(['/cupons']);
      }
    });
  }

  salvar() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.snack.open('Verifique os campos obrigatórios', 'Fechar', { duration: 3500 });
      return;
    }

    const raw = this.form.getRawValue();
    const payload: CupomDto = { ...raw, parceiroId: raw.parceiroId || null };
    this.loading = true;

    const successHandler = () => {
      this.loading = false;
      this.snack.open(this.editMode ? 'Cupom atualizado' : 'Cupom criado', 'Fechar', { duration: 3000 });
      this.router.navigate(['/cupons']);
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
    this.router.navigate(['/cupons']);
  }
}
