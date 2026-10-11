import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmTextareaImports } from '@spartan-ng/helm/textarea';
import { FormProtegido } from '../../core/form-guard';
import { NotificacaoService } from '../../core/notificacao.service';
import { crudForm } from '../../shared/form-fields/crud-form';
import { FormActionsComponent } from '../../shared/form-fields/form-actions.component';
import { FormErrorsComponent } from '../../shared/form-fields/form-errors.component';
import { FormFieldComponent } from '../../shared/form-fields/form-field.component';
import { FormSectionComponent } from '../../shared/form-fields/form-section.component';
import { FormSkeletonComponent } from '../../shared/form-fields/form-skeleton.component';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { MascaraDirective, mascararCnpj } from '../../shared/mascaras/mascara.directive';
import { MENSAGENS_CNPJ, VALIDADORES_CNPJ } from '../../shared/validators/documentos';
import { ClienteDto, ClientePessoaJuridicaDto } from './cliente.model';
import { ClientesService } from './clientes.service';

@Component({
  selector: 'app-cliente-pessoa-juridica-form',
  imports: [
    ReactiveFormsModule, HlmInputImports, HlmTextareaImports, MascaraDirective, PageComponent, PageHeaderComponent, FormSectionComponent,
    FormFieldComponent, FormActionsComponent, FormErrorsComponent, FormSkeletonComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cliente-pessoa-juridica-form.component.html'
})
export class ClientePessoaJuridicaFormComponent implements FormProtegido {
  private readonly svc = inject(ClientesService);
  private readonly router = inject(Router);
  private readonly notificacao = inject(NotificacaoService);

  readonly mensagensCnpj = MENSAGENS_CNPJ;

  readonly form = inject(NonNullableFormBuilder).group({
    nome: ['', Validators.required],
    cnpj: ['', VALIDADORES_CNPJ],
    nomeFantasia: [''],
    telefone: ['', Validators.required],
    email: ['', Validators.email],
    endereco: ['']
  });

  private readonly crud = crudForm<ClientePessoaJuridicaDto, typeof this.form.controls, ClienteDto>({
    form: this.form,
    obter: id => this.svc.obter(id),
    criar: payload => this.svc.criarPessoaJuridica(payload),
    atualizar: (id, payload) => this.svc.atualizarPessoaJuridica(id, payload),
    rotaLista: '/clientes',
    singular: 'cliente',
    aoCarregar: dado => this.preencher(dado)
  });
  readonly editMode = this.crud.editMode;
  readonly carregando = this.crud.carregando;
  readonly salvando = this.crud.salvando;
  readonly erros = this.crud.erros;
  readonly salvar = this.crud.salvar;
  readonly cancelar = this.crud.cancelar;

  private preencher(cliente: ClienteDto): void {
    if (cliente.tipoCliente !== 'PessoaJuridica') {
      this.notificacao.erro('Este cliente não é uma pessoa jurídica.');
      void this.router.navigate(['/clientes']);
      return;
    }
    this.form.patchValue({
      nome: cliente.nome,
      cnpj: mascararCnpj(cliente.cnpj),
      nomeFantasia: cliente.nomeFantasia ?? '',
      telefone: cliente.telefone,
      email: cliente.email ?? '',
      endereco: cliente.endereco ?? ''
    });
  }
}
