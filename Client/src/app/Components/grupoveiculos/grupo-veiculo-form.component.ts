import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmTextareaImports } from '@spartan-ng/helm/textarea';
import { FormProtegido } from '../../core/form-guard';
import { crudForm } from '../../shared/form-fields/crud-form';
import { FormActionsComponent } from '../../shared/form-fields/form-actions.component';
import { FormErrorsComponent } from '../../shared/form-fields/form-errors.component';
import { FormFieldComponent } from '../../shared/form-fields/form-field.component';
import { FormSectionComponent } from '../../shared/form-fields/form-section.component';
import { FormSkeletonComponent } from '../../shared/form-fields/form-skeleton.component';
import { PageHeaderComponent } from '../../shared/layout/page-header.component';
import { PageComponent } from '../../shared/layout/page.component';
import { GrupoVeiculoDto } from './grupo-veiculo.model';
import { GruposVeiculoService } from './grupos-veiculo.service';

@Component({
  selector: 'app-grupo-veiculo-form',
  imports: [
    ReactiveFormsModule, HlmInputImports, HlmTextareaImports, PageComponent, PageHeaderComponent, FormSectionComponent,
    FormFieldComponent, FormActionsComponent, FormErrorsComponent, FormSkeletonComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './grupo-veiculo-form.component.html'
})
export class GrupoVeiculoFormComponent implements FormProtegido {
  private readonly svc = inject(GruposVeiculoService);

  readonly form = inject(NonNullableFormBuilder).group({
    nome: ['', [Validators.required, Validators.maxLength(120)]],
    descricao: ['']
  });

  private readonly crud = crudForm<GrupoVeiculoDto, typeof this.form.controls>({
    form: this.form,
    obter: id => this.svc.obter(id),
    criar: payload => this.svc.criar(payload),
    atualizar: (id, payload) => this.svc.atualizar(id, payload),
    rotaLista: '/grupoveiculos',
    singular: 'grupo de veículo'
  });
  readonly editMode = this.crud.editMode;
  readonly carregando = this.crud.carregando;
  readonly salvando = this.crud.salvando;
  readonly erros = this.crud.erros;
  readonly salvar = this.crud.salvar;
  readonly cancelar = this.crud.cancelar;
}
