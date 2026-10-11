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
import { CupomDto } from '../cupons/cupom.model';
import { CuponsService } from '../cupons/cupons.service';
import { DesafioCupomDto } from './desafio-cupom.model';
import { DesafiosCupomService } from './desafios-cupom.service';

@Component({
  selector: 'app-desafio-cupom-form',
  imports: [
    ReactiveFormsModule, HlmInputImports, HlmNativeSelectImports, HlmCheckboxImports, HlmLabel, PageComponent, PageHeaderComponent,
    FormSectionComponent, FormFieldComponent, FormActionsComponent, FormErrorsComponent, FormSkeletonComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './desafio-cupom-form.component.html'
})
export class DesafioCupomFormComponent implements OnInit, FormProtegido {
  private readonly svc = inject(DesafiosCupomService);
  private readonly cuponsSvc = inject(CuponsService);

  readonly cupons = signal<CupomDto[]>([]);
  readonly mensagensMeta = { min: 'A meta deve ser de pelo menos 1 aluguel.' };
  readonly mensagensPeriodo = { min: 'O período deve ser de pelo menos 1 dia.' };
  readonly mensagensCupom = { required: 'Escolha o cupom de recompensa.' };

  readonly form = inject(NonNullableFormBuilder).group({
    nome: ['', Validators.required],
    descricao: ['', Validators.required],
    metaQuantidadeAlugueis: [1, [Validators.required, Validators.min(1)]],
    periodoDias: [30, [Validators.required, Validators.min(1)]],
    cupomRecompensaId: ['', Validators.required],
    ativo: [true]
  });

  private readonly crud = crudForm<DesafioCupomDto, typeof this.form.controls>({
    form: this.form,
    obter: id => this.svc.obter(id),
    criar: payload => this.svc.criar(payload),
    atualizar: (id, payload) => this.svc.atualizar(id, payload),
    rotaLista: '/desafios-cupom',
    singular: 'desafio',
    aoCarregar: d => this.form.patchValue({ ...d, cupomRecompensaId: d.cupomRecompensaId ?? '', ativo: d.ativo ?? true })
  });
  readonly editMode = this.crud.editMode;
  readonly carregando = this.crud.carregando;
  readonly salvando = this.crud.salvando;
  readonly erros = this.crud.erros;
  readonly salvar = this.crud.salvar;
  readonly cancelar = this.crud.cancelar;

  ngOnInit(): void {
    this.cuponsSvc.listar().subscribe({ next: c => this.cupons.set(c) });
  }
}
