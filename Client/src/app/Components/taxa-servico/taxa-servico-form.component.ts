import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmNativeSelectImports } from '@spartan-ng/helm/native-select';
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
import { TaxaServicoDto } from './taxa-servico.model';
import { TaxasServicosService } from './taxas-servicos.service';

@Component({
  selector: 'app-taxa-servico-form',
  imports: [
    ReactiveFormsModule, HlmInputImports, HlmNativeSelectImports, PageComponent, PageHeaderComponent, FormSectionComponent,
    FormFieldComponent, InputPrefixoComponent, FormActionsComponent, FormErrorsComponent, FormSkeletonComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './taxa-servico-form.component.html'
})
export class TaxaServicoFormComponent implements FormProtegido {
  private readonly svc = inject(TaxasServicosService);

  readonly mensagensPreco = { min: 'O preço deve ser maior ou igual a 0.' };

  readonly form = inject(NonNullableFormBuilder).group({
    nome: ['', Validators.required],
    preco: [0, [Validators.required, Validators.min(0)]],
    tipoCalculo: ['', Validators.required]
  });

  private readonly crud = crudForm<TaxaServicoDto, typeof this.form.controls>({
    form: this.form,
    obter: id => this.svc.obter(id),
    criar: payload => this.svc.criar(payload),
    atualizar: (id, payload) => this.svc.atualizar(id, payload),
    rotaLista: '/taxas-servicos',
    singular: 'taxa ou serviço',
    genero: 'f'
  });
  readonly editMode = this.crud.editMode;
  readonly carregando = this.crud.carregando;
  readonly salvando = this.crud.salvando;
  readonly erros = this.crud.erros;
  readonly salvar = this.crud.salvar;
  readonly cancelar = this.crud.cancelar;
}
