import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router, ActivatedRoute, ParamMap } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { FuncionariosService } from './funcionarios.service';
import { FuncionarioDto } from './funcionario.model';
import { Subscription } from 'rxjs';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-funcionario-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './funcionario-form.component.html',
  styleUrls: ['../scss/global.scss']
})
export class FuncionarioFormComponent implements OnInit, OnDestroy {
  form!: FormGroup;
  id?: string | null = null;
  loading = false;
  editMode = false;
  showPassword = false;
  showConfirmPassword = false;
  serverErrors: string[] = [];

  private sub: Subscription | null = null;

  constructor(
    private fb: FormBuilder,
    private svc: FuncionariosService,
    private route: ActivatedRoute,
    private router: Router,
    private snack: MatSnackBar
  ) {
    // Cria o formulário sem validador inicial
    this.form = this.fb.group({
      nomeCompleto: ['', Validators.required],
      cpf: ['', [Validators.required, Validators.pattern(/^\d{3}\.?\d{3}\.?\d{3}\-?\d{2}$/)]],
      email: ['', [Validators.email]],
      senha: ['', []],
      confirmarSenha: ['', []],
      salario: [0, [Validators.required, Validators.min(0)]],
      admissaoEmUtc: ['', Validators.required]
    });
  }

  ngOnInit() {
    this.sub = this.route.paramMap.subscribe((params: ParamMap) => {
      this.id = params.get('id');
      this.editMode = !!this.id;

      // Ajusta validações baseado no modo
      this.adjustValidations();

      if (this.editMode && this.id) {
        this.carregar(this.id);
      }
    });
  }

  ngOnDestroy() {
    this.sub?.unsubscribe();
  }

  private adjustValidations() {
    const senhaControl = this.form.get('senha');
    const confirmarSenhaControl = this.form.get('confirmarSenha');

    if (this.editMode) {
      // Em modo edição, senha não é obrigatória
      senhaControl?.clearValidators();
      confirmarSenhaControl?.clearValidators();

      // Remove o validador de comparação
      this.form.setValidators(null);
    } else {
      // Em modo criação, senha é obrigatória
      senhaControl?.setValidators([Validators.required, Validators.minLength(6)]);
      confirmarSenhaControl?.setValidators([Validators.required]);

      // Adiciona validador de comparação
      this.form.setValidators(this.createPasswordMatchValidator());
    }

    senhaControl?.updateValueAndValidity();
    confirmarSenhaControl?.updateValueAndValidity();
    this.form.updateValueAndValidity();
  }

  // Cria um validador que não depende do contexto 'this'
  private createPasswordMatchValidator() {
    return (form: AbstractControl): ValidationErrors | null => {
      const senha = form.get('senha')?.value;
      const confirmarSenha = form.get('confirmarSenha')?.value;

      // Só valida se ambos os campos estiverem preenchidos
      if (senha && confirmarSenha && senha !== confirmarSenha) {
        return { mismatch: true };
      }
      return null;
    };
  }

  toggleShowPassword() {
    this.showPassword = !this.showPassword;
  }

  toggleShowConfirmPassword() {
    this.showConfirmPassword = !this.showConfirmPassword;
  }

  carregar(id: string) {
    this.loading = true;
    this.svc.obter(id).subscribe({
      next: f => {
        // Remove senhas ao carregar para edição
        const { senha, confirmarSenha, ...dados } = f;
        this.form.patchValue(dados);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.snack.open('Erro ao carregar funcionário', 'Fechar', { duration: 4000 });
        this.router.navigate(['/funcionarios']);
      }
    });
  }

  salvar() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();

      if (this.form.hasError('mismatch')) {
        this.snack.open('As senhas não coincidem', 'Fechar', { duration: 3500 });
      } else {
        this.snack.open('Verifique os campos obrigatórios', 'Fechar', { duration: 3500 });
      }
      return;
    }

    let payload: FuncionarioDto = this.form.getRawValue();

    // Em modo edição, se senha estiver vazia, remove do payload
    if (this.editMode) {
      if (!payload.senha || payload.senha.trim() === '') {
        delete payload.senha;
        delete payload.confirmarSenha;
      }
    }

    this.loading = true;

    const successHandler = () => {
      this.loading = false;
      this.snack.open(this.editMode ? 'Funcionário atualizado' : 'Funcionário criado', 'Fechar', { duration: 3000 });
      this.router.navigate(['/funcionarios']);
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
    this.router.navigate(['/funcionarios']);
  }
}
