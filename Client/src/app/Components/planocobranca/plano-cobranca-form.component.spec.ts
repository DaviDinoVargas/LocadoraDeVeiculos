import { of, throwError } from 'rxjs';
import { montarFormCrud } from '../../core/testing/crud-form-harness.spec';
import { GruposVeiculoService } from '../grupoveiculos/grupos-veiculo.service';
import { PlanoCobrancaFormComponent } from './plano-cobranca-form.component';
import { PlanoCobrancaDto } from './plano-cobranca.model';
import { PlanosCobrancaService } from './planos-cobranca.service';
import { violacoesNosDoisTemas } from '../../core/testing/acessibilidade';

describe('PlanoCobrancaFormComponent — cadastro e edição', () => {
  let svc: jasmine.SpyObj<PlanosCobrancaService>;

  const valido = { nome: 'Diária Econômica', grupoAutomovelId: 'g1', precoDiaria: 100, precoPorKm: 0.5, kmLivreLimite: 100 };

  const montar = (id: string | null = null) => {
    svc = jasmine.createSpyObj('PlanosCobrancaService', ['obter', 'criar', 'atualizar']);
    svc.obter.and.returnValue(of({ id: 'pl1', ...valido } as unknown as PlanoCobrancaDto));
    svc.criar.and.returnValue(of({} as PlanoCobrancaDto));
    svc.atualizar.and.returnValue(of({} as PlanoCobrancaDto));
    return montarFormCrud(PlanoCobrancaFormComponent, {
      id,
      providers: [
        { provide: PlanosCobrancaService, useValue: svc },
        { provide: GruposVeiculoService, useValue: { listar: () => of([{ id: 'g1', nome: 'Econômico' }]) } }
      ]
    });
  };

  it('carrega os grupos de veículo', () => {
    const { component } = montar();

    expect(component.grupos().length).toBe(1);
  });

  it('formulário vazio não envia e preço negativo é recusado', () => {
    const { component, notificacao } = montar();

    component.salvar();
    expect(svc.criar).not.toHaveBeenCalled();
    expect(notificacao.aviso).toHaveBeenCalled();

    component.form.patchValue({ ...valido, precoDiaria: -5 });
    expect(component.form.controls.precoDiaria.hasError('min')).toBeTrue();
  });

  it('grupo é obrigatório', () => {
    const { component } = montar();
    component.form.patchValue({ ...valido, grupoAutomovelId: '' });

    expect(component.form.controls.grupoAutomovelId.hasError('required')).toBeTrue();
  });

  it('cria com os valores informados e volta para a lista', () => {
    const { component, router, notificacao } = montar();
    component.form.patchValue(valido);

    component.salvar();

    expect(svc.criar.calls.mostRecent().args[0].precoDiaria).toBe(100);
    expect(notificacao.sucesso).toHaveBeenCalledWith('Plano de cobrança criado');
    expect(router.navigate).toHaveBeenCalledWith(['/planos-cobranca']);
  });

  it('edita pelo id', () => {
    const { component } = montar('pl1');

    component.salvar();

    expect(svc.atualizar.calls.mostRecent().args[0]).toBe('pl1');
  });

  it('erro do servidor aparece no formulário', () => {
    const { component } = montar();
    svc.criar.and.returnValue(throwError(() => ({ status: 409, error: ['Já existe plano para este grupo.'] })));
    component.form.patchValue(valido);

    component.salvar();

    const campo = Object.entries(component.form.controls).find(([, c]) => c.errors?.['servidor']);
    expect(campo?.[0]).toBe('grupoAutomovelId');
    expect(campo?.[1].errors?.['servidor']).toBe('Já existe plano para este grupo.');
    expect(component.erros()).toEqual([]);
  });

  it('acessibilidade (axe-core, WCAG 2.2 AA): sem violações nos temas claro e escuro', async () => {
    const { fixture } = montar();
    expect(await violacoesNosDoisTemas(fixture.nativeElement)).toEqual([]);
  });
});
