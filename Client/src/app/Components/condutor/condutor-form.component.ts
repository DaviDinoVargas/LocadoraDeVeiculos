import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
import { FormProtegido } from '../../core/form-guard';
import { crudForm } from '../../shared/form-fields/crud-form';
import { FormActionsComponent } from '../../shared/form-fields/form-actions.component';
import { FormErrorsComponent } from '../../shared/form-fields/form-errors.component';
import { FormFieldComponent } from '../../shared/form-fields/form-field.component';
import { FormSectionComponent } from '../../shared/form-fields/form-section.component';
import { FormSkeletonComponent } from '../../shared/form-fields/form-skeleton.component';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { MascaraDirective } from '../../shared/mascaras/mascara.directive';
import { MENSAGENS_CPF, VALIDADORES_CPF } from '../../shared/validators/documentos';
import { ClienteDto } from '../clientes/cliente.model';
import { ClientesService } from '../clientes/clientes.service';
import { CondutorDto } from './condutor.model';
import { CondutoresService } from './condutores.service';

@Component({
  selector: 'app-condutor-form',
  imports: [
    ReactiveFormsModule, HlmInputImports, HlmNativeSelectImports, MascaraDirective, PageComponent, PageHeaderComponent,
    FormSectionComponent, FormFieldComponent, FormActionsComponent, FormErrorsComponent, FormSkeletonComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './condutor-form.component.html'
})
export class CondutorFormComponent implements OnInit, FormProtegido {
  private readonly svc = inject(CondutoresService);
  private readonly clientesSvc = inject(ClientesService);

  readonly clientes = signal<ClienteDto[]>([]);
  readonly mensagensCpf = MENSAGENS_CPF;

  readonly form = inject(NonNullableFormBuilder).group({
    nome: ['', Validators.required],
    cpf: ['', VALIDADORES_CPF],
    cnh: ['', Validators.required],
    validadeCnh: ['', Validators.required],
    telefone: ['', Validators.required],
    email: ['', Validators.email],
    clienteId: ['', Validators.required]
  });

  private readonly crud = crudForm<CondutorDto, typeof this.form.controls>({
    form: this.form,
    obter: id => this.svc.obter(id),
    criar: payload => this.svc.criar(payload),
    atualizar: (id, payload) => this.svc.atualizar(id, payload),
    rotaLista: '/condutores',
    singular: 'condutor',
    aoCarregar: c =>
      this.form.patchValue({ ...c, email: c.email ?? '', validadeCnh: (c.validadeCnh ?? '').slice(0, 10) })
  });
  readonly editMode = this.crud.editMode;
  readonly carregando = this.crud.carregando;
  readonly salvando = this.crud.salvando;
  readonly erros = this.crud.erros;
  readonly salvar = this.crud.salvar;
  readonly cancelar = this.crud.cancelar;

  ngOnInit(): void {
    this.clientesSvc.listar().subscribe({ next: clientes => this.clientes.set(clientes) });
  }
}
