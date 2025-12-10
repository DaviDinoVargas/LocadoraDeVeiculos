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
      cpf: ['', [Validators.required, Validators.pattern(/^\d{3}\.\d{3}\.\d{3}-\d{2}$/)]], // Formato com pontos e traço
      rg: [''],
      cnh: [''],
      validadeCnh: [''],
      telefone: ['', Validators.required],
      email: ['', [Validators.email]],
      endereco: [''],
      clientePessoaJuridicaId: [null]
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

        // Formatar CPF se necessário
        let cpfFormatado = cliente.cpf;
        if (cpfFormatado && !cpfFormatado.includes('.')) {
          cpfFormatado = this.formatarCPFString(cpfFormatado);
        }

        this.form.patchValue({
          ...cliente,
          cpf: cpfFormatado,
          clientePessoaJuridicaId: cliente.clientePessoaJuridicaId || null
        });
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snack.open('Erro ao carregar cliente', 'Fechar', { duration: 4000 });
        this.router.navigate(['/clientes']);
      }
    });
  }

  // Método para formatar string de CPF
  formatarCPFString(cpf: string): string {
    if (!cpf) return '';
    cpf = cpf.replace(/\D/g, '');
    return cpf.replace(/(\d{3})(\d{3})(\d{3})(\d{2})/, '$1.$2.$3-$4');
  }

  // Método para formatar durante a digitação
  formatarCPFInput(event: any) {
    let value = event.target.value.replace(/\D/g, '');

    if (value.length > 11) {
      value = value.substring(0, 11);
    }

    // Formatar CPF: 000.000.000-00
    if (value.length <= 11) {
      value = value.replace(/(\d{3})(\d)/, '$1.$2');
      value = value.replace(/(\d{3})(\d)/, '$1.$2');
      value = value.replace(/(\d{3})(\d{1,2})$/, '$1-$2');
    }

    event.target.value = value;
    this.form.get('cpf')?.setValue(value, { emitEvent: false });
  }

  // Método para formatar RG durante a digitação
  formatarRGInput(event: any) {
    let value = event.target.value.replace(/\D/g, '');

    if (value.length > 9) {
      value = value.substring(0, 9);
    }

    // Formatar RG: 00.000.000-0
    if (value.length <= 9) {
      value = value.replace(/(\d{2})(\d)/, '$1.$2');
      value = value.replace(/(\d{3})(\d)/, '$1.$2');
      value = value.replace(/(\d{3})(\d{1})$/, '$1-$2');
    }

    event.target.value = value;
    this.form.get('rg')?.setValue(value, { emitEvent: false });
  }

  // Método para formatar telefone
  formatarTelefoneInput(event: any) {
    let value = event.target.value.replace(/\D/g, '');

    if (value.length > 11) {
      value = value.substring(0, 11);
    }

    // Formatar telefone: (00) 00000-0000
    if (value.length === 11) {
      value = value.replace(/(\d{2})(\d{5})(\d{4})/, '($1) $2-$3');
    } else if (value.length === 10) {
      value = value.replace(/(\d{2})(\d{4})(\d{4})/, '($1) $2-$3');
    }

    event.target.value = value;
    this.form.get('telefone')?.setValue(value, { emitEvent: false });
  }

  formatarCNPJ(cnpj: string): string {
    if (!cnpj) return '';
    cnpj = cnpj.replace(/\D/g, '');
    return cnpj.replace(/(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})/, '$1.$2.$3/$4-$5');
  }

  salvar() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();

      // Verificar erros específicos
      if (this.form.get('cpf')?.invalid) {
        this.snack.open('CPF inválido. Use o formato 000.000.000-00', 'Fechar', { duration: 4000 });
      } else if (this.form.get('email')?.invalid) {
        this.snack.open('Email inválido', 'Fechar', { duration: 4000 });
      } else {
        this.snack.open('Verifique os campos obrigatórios', 'Fechar', { duration: 3500 });
      }

      return;
    }

    const formValue = this.form.getRawValue();

    // Criar payload com dados formatados
    const payload: any = {
      nome: formValue.nome,
      cpf: formValue.cpf, // Já está formatado com pontos e traço
      telefone: formValue.telefone
    };

    // Adicionar campos opcionais apenas se preenchidos
    if (formValue.rg && formValue.rg.trim() !== '') {
      payload.rg = formValue.rg;
    }

    if (formValue.cnh && formValue.cnh.trim() !== '') {
      payload.cnh = formValue.cnh;
    }

    if (formValue.validadeCnh && formValue.validadeCnh.trim() !== '') {
      payload.validadeCnh = formValue.validadeCnh;
    }

    if (formValue.email && formValue.email.trim() !== '') {
      payload.email = formValue.email;
    }

    if (formValue.endereco && formValue.endereco.trim() !== '') {
      payload.endereco = formValue.endereco;
    }

    // Enviar null se não houver vínculo
    if (formValue.clientePessoaJuridicaId) {
      payload.clientePessoaJuridicaId = formValue.clientePessoaJuridicaId;
    } else {
      payload.clientePessoaJuridicaId = null;
    }

    // Log para debug
    console.log('Payload sendo enviado:', JSON.stringify(payload, null, 2));
    console.log('CPF sendo enviado:', payload.cpf);

    this.loading = true;
    this.serverErrors = [];

    const successHandler = () => {
      this.loading = false;
      this.snack.open(this.editMode ? 'Cliente atualizado' : 'Cliente criado', 'Fechar', { duration: 3000 });
      this.router.navigate(['/clientes']);
    };

    const errorHandler = (err: any) => {
      this.loading = false;

      console.error('Erro completo:', err);

      if (err.error && Array.isArray(err.error)) {
        // A API está retornando array de strings
        this.serverErrors = err.error;
      } else if (err.error && err.error.errors) {
        // Formato de erros de validação
        this.serverErrors = [];
        Object.keys(err.error.errors).forEach(key => {
          err.error.errors[key].forEach((message: string) => {
            this.serverErrors.push(`${key}: ${message}`);
          });
        });
      } else {
        this.serverErrors = [err?.message || 'Erro desconhecido'];
      }

      this.snack.open(this.serverErrors[0] || 'Erro ao salvar cliente', 'Fechar', { duration: 4000 });
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
