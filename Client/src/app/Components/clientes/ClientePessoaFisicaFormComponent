import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, ParamMap } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ClientesService } from './clientes.service';
import { ClientePessoaFisicaDto, ClientePessoaJuridicaDto } from './cliente.model';
import { Subscription } from 'rxjs';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-cliente-pessoa-fisica-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './cliente-pessoa-fisica-form.component.html',
  styleUrls: ['../scss/global.scss']
})
export class ClientePessoaFisicaFormComponent implements OnInit, OnDestroy {
  form!: FormGroup;
  id?: string | null = null;
  loading = false;
  editMode = false;
  serverErrors: string[] = [];
  pessoasJuridicas: ClientePessoaJuridicaDto[] = [];

  private sub: Subscription | null = null;

  constructor(
    private fb: FormBuilder,
    private svc: ClientesService,
    private route: ActivatedRoute,
    private router: Router,
    private snack: MatSnackBar
  ) {
    this.form = this.fb.group({
      nome: ['', Validators.required],
      cpf: ['', [Validators.required, Validators.pattern(/^\d{3}\.?\d{3}\.?\d{3}\-?\d{2}$/)]],
      rg: [''],
      cnh: [''],
      validadeCnh: [''],
      telefone: ['', Validators.required],
      email: ['', Validators.email],
      endereco: [''],
      clientePessoaJuridicaId: ['']
    });
  }

  ngOnInit() {
    this.carregarPessoasJuridicas();

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

  carregarPessoasJuridicas() {
    this.svc.listarPessoasJuridicas().subscribe({
      next: pj => this.pessoasJuridicas = pj,
      error: () => this.snack.open('Erro ao carregar pessoas jurídicas', 'Fechar', { duration: 4000 })
    });
  }

  carregar(id: string) {
    this.loading = true;
    this.svc.obter(id).subscribe({
      next: (cliente: any) => {
        if (cliente.tipoCliente !== 'PessoaFisica') {
          this.snack.open('Cliente não é uma pessoa física', 'Fechar', { duration: 4000 });
          this.router.navigate(['/clientes']);
          return;
        }
        this.form.patchValue(cliente);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snack.open('Erro ao carregar cliente', 'Fechar', { duration: 4000 });
        this.router.navigate(['/clientes']);
      }
    });
  }

  formatarCNPJ(cnpj: string): string {
    if (!cnpj) return '';
    return cnpj.replace(/(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})/, '$1.$2.$3/$4-$5');
  }

  salvar() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.snack.open('Verifique os campos obrigatórios', 'Fechar', { duration: 3500 });
      return;
    }

    const payload: ClientePessoaFisicaDto = this.form.getRawValue();
    this.loading = true;

    const successHandler = () => {
      this.loading = false;
      this.snack.open(this.editMode ? 'Cliente atualizado' : 'Cliente criado', 'Fechar', { duration: 3000 });
      this.router.navigate(['/clientes']);
    };

    const errorHandler = (err: any) => {
      this.loading = false;
      this.serverErrors = [err?.message ?? 'Erro desconhecido'];
      this.snack.open(this.serverErrors[0], 'Fechar', { duration: 4000 });
    };

    if (this.editMode && this.id) {
      this.svc.atualizarPessoaFisica(this.id, payload).subscribe({
        next: successHandler,
        error: errorHandler
      });
    } else {
      this.svc.criarPessoaFisica(payload).subscribe({
        next: successHandler,
        error: errorHandler
      });
    }
  }

  cancelar() {
    this.router.navigate(['/clientes']);
  }
}
