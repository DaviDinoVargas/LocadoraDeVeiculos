import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { FormProtegido } from '../../core/form-guard';
import { crudForm } from '../../shared/form-fields/crud-form';
import { FormActionsComponent } from '../../shared/form-fields/form-actions.component';
import { FormErrorsComponent } from '../../shared/form-fields/form-errors.component';
import { FormFieldComponent } from '../../shared/form-fields/form-field.component';
import { InputPrefixoComponent } from '../../shared/form-fields/input-prefixo.component';
import { FormSectionComponent } from '../../shared/form-fields/form-section.component';
import { FormSkeletonComponent } from '../../shared/form-fields/form-skeleton.component';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { MascaraDirective } from '../../shared/mascaras/mascara.directive';
import { MENSAGENS_CPF, VALIDADORES_CPF } from '../../shared/validators/documentos';
import { FuncionarioDto } from './funcionario.model';
import { FuncionariosService } from './funcionarios.service';

/** Erro `mismatch` na confirmação (não no grupo) para a mensagem aparecer embaixo do campo certo. */
function senhasIguais(grupo: AbstractControl): ValidationErrors | null {
  const confirmar = grupo.get('confirmarSenha');
  if (!confirmar) return null;
  const senha = grupo.get('senha')?.value;
  const diferente = !!senha && !!confirmar.value && senha !== confirmar.value;
  const erros = { ...(confirmar.errors ?? {}) };
  if (diferente) {
    erros['mismatch'] = true;
  } else {
    delete erros['mismatch'];
  }
  confirmar.setErrors(Object.keys(erros).length ? erros : null, { emitEvent: false });
  return null;
}

@Component({
  selector: 'app-funcionario-form',
  imports: [
    ReactiveFormsModule, NgIcon, HlmButtonImports, HlmInputImports, MascaraDirective, PageComponent, PageHeaderComponent,
    FormSectionComponent, FormFieldComponent, InputPrefixoComponent, FormActionsComponent, FormErrorsComponent, FormSkeletonComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './funcionario-form.component.html'
})
export class FuncionarioFormComponent implements FormProtegido {
  private readonly svc = inject(FuncionariosService);

  readonly mensagensCpf = MENSAGENS_CPF;
  readonly mensagensSalario = { min: 'O salário deve ser maior ou igual a 0.' };
  readonly mensagensSenha = { minlength: 'A senha precisa de ao menos 6 caracteres.' };
  readonly mensagensConfirmacao = { mismatch: 'As senhas não coincidem.' };
  readonly mostrarSenha = signal(false);
  readonly mostrarConfirmacao = signal(false);

  readonly form = inject(NonNullableFormBuilder).group({
    nomeCompleto: ['', Validators.required],
    cpf: ['', VALIDADORES_CPF],
    email: ['', [Validators.email]],
    senha: [''],
    confirmarSenha: [''],
    salario: [0, [Validators.required, Validators.min(0)]],
    admissaoEmUtc: ['', Validators.required]
  });

  private readonly crud = crudForm<FuncionarioDto, typeof this.form.controls>({
    form: this.form,
    obter: id => this.svc.obter(id),
    criar: payload => this.svc.criar(payload),
    atualizar: (id, payload) => this.svc.atualizar(id, payload),
    rotaLista: '/funcionarios',
    singular: 'funcionário',
    // Nunca preenche as senhas ao editar (campos sensíveis: só se digitar uma nova)
    aoCarregar: f =>
      this.form.patchValue({
        nomeCompleto: f.nomeCompleto,
        cpf: f.cpf,
        email: f.email ?? '',
        salario: f.salario,
        admissaoEmUtc: (f.admissaoEmUtc ?? '').slice(0, 10)
      }),
    montarPayload: () => this.montarPayload()
  });
  readonly editMode = this.crud.editMode;
  readonly carregando = this.crud.carregando;
  readonly salvando = this.crud.salvando;
  readonly erros = this.crud.erros;
  readonly salvar = this.crud.salvar;
  readonly cancelar = this.crud.cancelar;

  constructor() {
    // O modo vem da rota já na criação: na edição a senha é opcional; no cadastro é obrigatória.
    const editando = !!inject(ActivatedRoute).snapshot.paramMap.get('id');
    if (!editando) {
      this.form.controls.senha.setValidators([Validators.required, Validators.minLength(6)]);
      this.form.controls.confirmarSenha.setValidators([Validators.required]);
    } else {
      this.form.controls.senha.setValidators([Validators.minLength(6)]);
    }
    this.form.addValidators(senhasIguais);
    this.form.updateValueAndValidity();
  }

  alternarSenha(): void {
    this.mostrarSenha.update(v => !v);
  }

  alternarConfirmacao(): void {
    this.mostrarConfirmacao.update(v => !v);
  }

  // Em edição, senha em branco não é enviada (mantém a atual)
  private montarPayload(): FuncionarioDto {
    const payload: FuncionarioDto = this.form.getRawValue();
    if (this.editMode() && !payload.senha?.trim()) {
      delete payload.senha;
      delete payload.confirmarSenha;
    }
    return payload;
  }
}
