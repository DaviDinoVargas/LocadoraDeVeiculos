import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
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
import { GrupoVeiculoDto } from '../grupoveiculos/grupo-veiculo.model';
import { GruposVeiculoService } from '../grupoveiculos/grupos-veiculo.service';
import { PlanoCobrancaDto } from './plano-cobranca.model';
import { PlanosCobrancaService } from './planos-cobranca.service';

@Component({
  selector: 'app-plano-cobranca-form',
  imports: [
    ReactiveFormsModule, HlmInputImports, HlmNativeSelectImports, PageComponent, PageHeaderComponent, FormSectionComponent,
    FormFieldComponent, InputPrefixoComponent, FormActionsComponent, FormErrorsComponent, FormSkeletonComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './plano-cobranca-form.component.html'
})
export class PlanoCobrancaFormComponent implements OnInit, FormProtegido {
  private readonly svc = inject(PlanosCobrancaService);
  private readonly gruposSvc = inject(GruposVeiculoService);

  readonly grupos = signal<GrupoVeiculoDto[]>([]);
  readonly mensagensPreco = { min: 'O valor deve ser maior ou igual a 0.' };

  readonly form = inject(NonNullableFormBuilder).group({
    nome: ['', Validators.required],
    grupoAutomovelId: ['', Validators.required],
    precoDiaria: [0, [Validators.required, Validators.min(0)]],
    precoPorKm: [0, [Validators.min(0)]],
    kmLivreLimite: [0, [Validators.min(0)]]
  });

  private readonly crud = crudForm<PlanoCobrancaDto, typeof this.form.controls>({
    form: this.form,
    obter: id => this.svc.obter(id),
    criar: payload => this.svc.criar(payload),
    atualizar: (id, payload) => this.svc.atualizar(id, payload),
    rotaLista: '/planos-cobranca',
    singular: 'plano de cobrança',
    aoCarregar: p =>
      this.form.patchValue({
        nome: p.nome,
        grupoAutomovelId: p.grupoAutomovelId,
        precoDiaria: p.precoDiaria,
        precoPorKm: p.precoPorKm ?? 0,
        kmLivreLimite: p.kmLivreLimite ?? 0
      })
  });
  readonly editMode = this.crud.editMode;
  readonly carregando = this.crud.carregando;
  readonly salvando = this.crud.salvando;
  readonly erros = this.crud.erros;
  readonly salvar = this.crud.salvar;
  readonly cancelar = this.crud.cancelar;

  ngOnInit(): void {
    this.gruposSvc.listar().subscribe({ next: grupos => this.grupos.set(grupos) });
  }
}
