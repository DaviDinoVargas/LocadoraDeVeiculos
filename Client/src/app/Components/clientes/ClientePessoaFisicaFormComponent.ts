import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
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
import { MascaraDirective, mascararCnpj, mascararCpf } from '../../shared/mascaras/mascara.directive';
import { MENSAGENS_CPF, VALIDADORES_CPF } from '../../shared/validators/documentos';
import { ClienteDto, ClientePessoaFisicaDto, ClientePessoaJuridicaDto } from './cliente.model';
import { ClientesService } from './clientes.service';

@Component({
  selector: 'app-cliente-pessoa-fisica-form',
  imports: [
    ReactiveFormsModule, HlmInputImports, HlmNativeSelectImports, HlmTextareaImports, MascaraDirective, PageComponent, PageHeaderComponent,
    FormSectionComponent, FormFieldComponent, FormActionsComponent, FormErrorsComponent, FormSkeletonComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cliente-pessoa-fisica-form.component.html'
})
export class ClientePessoaFisicaFormComponent implements OnInit, FormProtegido {
  private readonly svc = inject(ClientesService);
  private readonly router = inject(Router);
  private readonly notificacao = inject(NotificacaoService);

  readonly pessoasJuridicas = signal<ClientePessoaJuridicaDto[]>([]);
  readonly mensagensCpf = MENSAGENS_CPF;

  readonly form = inject(NonNullableFormBuilder).group({
    nome: ['', Validators.required],
    cpf: ['', VALIDADORES_CPF],
    rg: [''],
    cnh: [''],
    validadeCnh: [''],
    telefone: ['', Validators.required],
    email: ['', [Validators.email]],
    endereco: [''],
    clientePessoaJuridicaId: ['']
  });

  private readonly crud = crudForm<ClientePessoaFisicaDto, typeof this.form.controls, ClienteDto>({
    form: this.form,
    obter: id => this.svc.obter(id),
    criar: payload => this.svc.criarPessoaFisica(payload),
    atualizar: (id, payload) => this.svc.atualizarPessoaFisica(id, payload),
    rotaLista: '/clientes',
    singular: 'cliente',
    aoCarregar: dado => this.preencher(dado),
    montarPayload: () => this.montarPayload()
  });
  readonly editMode = this.crud.editMode;
  readonly carregando = this.crud.carregando;
  readonly salvando = this.crud.salvando;
  readonly erros = this.crud.erros;
  readonly salvar = this.crud.salvar;
  readonly cancelar = this.crud.cancelar;

  ngOnInit(): void {
    this.svc.listarPessoasJuridicas().subscribe({ next: pj => this.pessoasJuridicas.set(pj) });
  }

  formatarCNPJ(cnpj: string): string {
    return mascararCnpj(cnpj);
  }

  private preencher(cliente: ClienteDto): void {
    if (cliente.tipoCliente !== 'PessoaFisica') {
      this.notificacao.erro('Este cliente não é uma pessoa física.');
      void this.router.navigate(['/clientes']);
      return;
    }
    this.form.patchValue({
      nome: cliente.nome,
      cpf: mascararCpf(cliente.cpf),
      rg: cliente.rg ?? '',
      cnh: cliente.cnh ?? '',
      validadeCnh: (cliente.validadeCnh ?? '').slice(0, 10),
      telefone: cliente.telefone,
      email: cliente.email ?? '',
      endereco: cliente.endereco ?? '',
      clientePessoaJuridicaId: cliente.clientePessoaJuridicaId ?? ''
    });
  }

  // Só envia os opcionais preenchidos; sem vínculo com PJ vai null (a API distingue "sem vínculo").
  private montarPayload(): ClientePessoaFisicaDto {
    const v = this.form.getRawValue();
    const payload: ClientePessoaFisicaDto = { nome: v.nome, cpf: v.cpf, telefone: v.telefone };
    if (v.rg.trim()) payload.rg = v.rg;
    if (v.cnh.trim()) payload.cnh = v.cnh;
    if (v.validadeCnh.trim()) payload.validadeCnh = v.validadeCnh;
    if (v.email.trim()) payload.email = v.email;
    if (v.endereco.trim()) payload.endereco = v.endereco;
    payload.clientePessoaJuridicaId = v.clientePessoaJuridicaId || null;
    return payload;
  }
}
