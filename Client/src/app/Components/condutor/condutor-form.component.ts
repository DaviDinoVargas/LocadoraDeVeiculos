import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, ParamMap } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CondutoresService } from './condutores.service';
import { ClientesService } from '../clientes/clientes.service';
import { CondutorDto } from './condutor.model';
import { ClienteDto } from '../clientes/cliente.model';
import { Subscription } from 'rxjs';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-condutor-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './condutor-form.component.html',
  styleUrls: ['../scss/global.scss']
})
export class CondutorFormComponent implements OnInit, OnDestroy {
  form!: FormGroup;
  id?: string | null = null;
  loading = false;
  editMode = false;
  serverErrors: string[] = [];
  clientes: ClienteDto[] = [];

  private sub: Subscription | null = null;

  constructor(
    private fb: FormBuilder,
    private svc: CondutoresService,
    private clientesSvc: ClientesService,
    private route: ActivatedRoute,
    private router: Router,
    private snack: MatSnackBar
  ) {
    this.form = this.fb.group({
      nome: ['', Validators.required],
      cpf: ['', [Validators.required, Validators.pattern(/^\d{3}\.?\d{3}\.?\d{3}\-?\d{2}$/)]],
      cnh: ['', Validators.required],
      validadeCnh: ['', Validators.required],
      telefone: ['', Validators.required],
      email: ['', Validators.email],
      clienteId: ['', Validators.required]
    });
  }

  ngOnInit() {
    this.carregarClientes();

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

  carregarClientes() {
    this.clientesSvc.listar().subscribe({
      next: clientes => this.clientes = clientes,
      error: () => this.snack.open('Erro ao carregar clientes', 'Fechar', { duration: 4000 })
    });
  }

  carregar(id: string) {
    this.loading = true;
    this.svc.obter(id).subscribe({
      next: c => {
        this.form.patchValue(c);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snack.open('Erro ao carregar condutor', 'Fechar', { duration: 4000 });
        this.router.navigate(['/condutores']);
      }
    });
  }

  salvar() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.snack.open('Verifique os campos obrigatórios', 'Fechar', { duration: 3500 });
      return;
    }

    const payload: CondutorDto = this.form.getRawValue();
    this.loading = true;

    const successHandler = () => {
      this.loading = false;
      this.snack.open(this.editMode ? 'Condutor atualizado' : 'Condutor criado', 'Fechar', { duration: 3000 });
      this.router.navigate(['/condutores']);
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
    this.router.navigate(['/condutores']);
  }
}
