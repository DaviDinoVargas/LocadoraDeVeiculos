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
import { GrupoVeiculoDto } from '../grupoveiculos/grupo-veiculo.model';
import { GruposVeiculoService } from '../grupoveiculos/grupos-veiculo.service';
import { VeiculoDto } from './veiculo.model';
import { VeiculosService } from './veiculos.service';

@Component({
  selector: 'app-veiculo-form',
  imports: [
    ReactiveFormsModule, HlmInputImports, HlmNativeSelectImports, MascaraDirective, PageComponent, PageHeaderComponent, FormSectionComponent,
    FormFieldComponent, FormActionsComponent, FormErrorsComponent, FormSkeletonComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './veiculo-form.component.html'
})
export class VeiculoFormComponent implements OnInit, FormProtegido {
  private readonly svc = inject(VeiculosService);
  private readonly gruposSvc = inject(GruposVeiculoService);

  readonly anoAtual = new Date().getFullYear();
  readonly combustiveis = ['Gasolina', 'Álcool', 'Diesel', 'Flex', 'Elétrico', 'Híbrido'];
  readonly grupos = signal<GrupoVeiculoDto[]>([]);
  readonly mensagensPlaca = { pattern: 'Placa inválida. Use o formato AAA-0000 ou AAA-0A00.' };
  readonly mensagensAno = { min: 'O ano mínimo é 1900.', max: `O ano máximo é ${this.anoAtual + 1}.` };

  readonly form = inject(NonNullableFormBuilder).group({
    placa: ['', [Validators.required, Validators.pattern(/^[A-Z]{3}-[0-9][A-Z0-9][0-9]{2}$/)]],
    marca: ['', Validators.required],
    modelo: ['', Validators.required],
    cor: ['', Validators.required],
    ano: [this.anoAtual, [Validators.required, Validators.min(1900), Validators.max(this.anoAtual + 1)]],
    tipoCombustivel: ['', Validators.required],
    capacidadeTanque: [0, [Validators.required, Validators.min(0.1)]],
    grupoAutomovelId: ['', Validators.required],
    foto: ['']
  });

  private readonly crud = crudForm<VeiculoDto, typeof this.form.controls>({
    form: this.form,
    obter: id => this.svc.obter(id),
    criar: payload => this.svc.criar(payload),
    atualizar: (id, payload) => this.svc.atualizar(id, payload),
    rotaLista: '/veiculos',
    singular: 'veículo',
    aoCarregar: v => this.form.patchValue(v)
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
