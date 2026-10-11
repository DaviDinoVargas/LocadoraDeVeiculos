import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HlmCheckboxImports } from '@spartan-ng/helm/checkbox';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { HlmLabel } from '@spartan-ng/helm/label';
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
import { MENSAGENS_CNPJ, VALIDADORES_CNPJ } from '../../shared/validators/documentos';
import { CATEGORIAS_PARCEIRO, ParceiroDto } from './parceiro.model';
import { ParceirosService } from './parceiros.service';

@Component({
  selector: 'app-parceiro-form',
  imports: [
    ReactiveFormsModule, HlmInputImports, HlmNativeSelectImports, HlmCheckboxImports, HlmLabel, MascaraDirective, PageComponent,
    PageHeaderComponent, FormSectionComponent, FormFieldComponent, FormActionsComponent, FormErrorsComponent, FormSkeletonComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './parceiro-form.component.html'
})
export class ParceiroFormComponent implements FormProtegido {
  private readonly svc = inject(ParceirosService);

  readonly categorias = CATEGORIAS_PARCEIRO;
  readonly mensagensCnpj = MENSAGENS_CNPJ;

  readonly form = inject(NonNullableFormBuilder).group({
    nome: ['', Validators.required],
    cnpj: ['', VALIDADORES_CNPJ],
    categoria: ['', Validators.required],
    ativo: [true]
  });

  private readonly crud = crudForm<ParceiroDto, typeof this.form.controls>({
    form: this.form,
    obter: id => this.svc.obter(id),
    criar: payload => this.svc.criar(payload),
    atualizar: (id, payload) => this.svc.atualizar(id, payload),
    rotaLista: '/parceiros',
    singular: 'parceiro',
    aoCarregar: p => this.form.patchValue({ ...p, ativo: p.ativo ?? true })
  });
  readonly editMode = this.crud.editMode;
  readonly carregando = this.crud.carregando;
  readonly salvando = this.crud.salvando;
  readonly erros = this.crud.erros;
  readonly salvar = this.crud.salvar;
  readonly cancelar = this.crud.cancelar;
}
