import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
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
import { ParceiroDto } from '../parceiros/parceiro.model';
import { ParceirosService } from '../parceiros/parceiros.service';
import { CupomDto, TIPOS_DESCONTO } from './cupom.model';
import { CuponsService } from './cupons.service';

@Component({
  selector: 'app-cupom-form',
  imports: [
    ReactiveFormsModule, HlmInputImports, HlmNativeSelectImports, HlmCheckboxImports, HlmLabel, PageComponent, PageHeaderComponent,
    FormSectionComponent, FormFieldComponent, FormActionsComponent, FormErrorsComponent, FormSkeletonComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cupom-form.component.html'
})
export class CupomFormComponent implements OnInit, FormProtegido {
  private readonly svc = inject(CuponsService);
  private readonly parceirosSvc = inject(ParceirosService);

  readonly tiposDesconto = TIPOS_DESCONTO;
  readonly parceiros = signal<ParceiroDto[]>([]);
  readonly mensagensValor = { min: 'O valor deve ser maior ou igual a 0.' };
  readonly mensagensLimite = { min: 'O limite deve ser de pelo menos 1 uso.' };

  readonly form = inject(NonNullableFormBuilder).group({
    codigo: ['', Validators.required],
    descricao: ['', Validators.required],
    tipoDesconto: ['', Validators.required],
    valorDesconto: [0, [Validators.required, Validators.min(0)]],
    validoAte: ['', Validators.required],
    limiteUsos: [1, [Validators.required, Validators.min(1)]],
    parceiroId: [''],
    ativo: [true]
  });

  private readonly crud = crudForm<CupomDto, typeof this.form.controls>({
    form: this.form,
    obter: id => this.svc.obter(id),
    criar: payload => this.svc.criar(payload),
    atualizar: (id, payload) => this.svc.atualizar(id, payload),
    rotaLista: '/cupons',
    singular: 'cupom',
    aoCarregar: c =>
      this.form.patchValue({
        ...c,
        validoAte: c.validoAte ? c.validoAte.substring(0, 10) : '',
        parceiroId: c.parceiroId ?? '',
        ativo: c.ativo ?? true
      }),
    montarPayload: () => {
      const raw = this.form.getRawValue();
      return { ...raw, parceiroId: raw.parceiroId || null };
    }
  });
  readonly editMode = this.crud.editMode;
  readonly carregando = this.crud.carregando;
  readonly salvando = this.crud.salvando;
  readonly erros = this.crud.erros;
  readonly salvar = this.crud.salvar;
  readonly cancelar = this.crud.cancelar;

  ngOnInit(): void {
    this.parceirosSvc.listar().subscribe({ next: p => this.parceiros.set(p) });
  }
}
