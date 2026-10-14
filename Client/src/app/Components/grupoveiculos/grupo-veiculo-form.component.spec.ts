import { of, throwError } from 'rxjs';
import { montarFormCrud } from '../../core/testing/crud-form-harness.spec';
import { GrupoVeiculoFormComponent } from './grupo-veiculo-form.component';
import { GrupoVeiculoDto } from './grupo-veiculo.model';
import { GruposVeiculoService } from './grupos-veiculo.service';

describe('GrupoVeiculoFormComponent — cadastro e edição', () => {
  let svc: jasmine.SpyObj<GruposVeiculoService>;

  const montar = (id: string | null = null) => {
    svc = jasmine.createSpyObj('GruposVeiculoService', ['obter', 'criar', 'atualizar']);
    svc.obter.and.returnValue(of({ id: 'g1', nome: 'SUV', descricao: 'Utilitários' } as unknown as GrupoVeiculoDto));
    svc.criar.and.returnValue(of({} as GrupoVeiculoDto));
    svc.atualizar.and.returnValue(of({} as GrupoVeiculoDto));
    return montarFormCrud(GrupoVeiculoFormComponent, { id, providers: [{ provide: GruposVeiculoService, useValue: svc }] });
  };

  it('nome é obrigatório e limitado a 120 caracteres', () => {
    const { component } = montar();

    expect(component.form.controls.nome.hasError('required')).toBeTrue();
    component.form.patchValue({ nome: 'x'.repeat(121) });
    expect(component.form.controls.nome.hasError('maxlength')).toBeTrue();
  });

  it('formulário vazio não envia', () => {
    const { component, notificacao } = montar();

    component.salvar();

    expect(svc.criar).not.toHaveBeenCalled();
    expect(notificacao.aviso).toHaveBeenCalled();
  });

  it('cria e volta para a lista', () => {
    const { component, router, notificacao } = montar();
    component.form.patchValue({ nome: 'Econômico', descricao: '' });

    component.salvar();

    expect(svc.criar.calls.mostRecent().args[0].nome).toBe('Econômico');
    expect(notificacao.sucesso).toHaveBeenCalledWith('Grupo de veículo criado');
    expect(router.navigate).toHaveBeenCalledWith(['/grupoveiculos']);
  });

  it('edita: carrega o grupo e atualiza pelo id', () => {
    const { component } = montar('g1');

    expect(component.form.controls.nome.value).toBe('SUV');
    component.salvar();

    expect(svc.atualizar.calls.mostRecent().args[0]).toBe('g1');
  });

  it('erro do servidor aparece no formulário', () => {
    const { component } = montar();
    svc.criar.and.returnValue(throwError(() => ({ status: 409, error: ['Já existe um grupo com este nome.'] })));
    component.form.patchValue({ nome: 'SUV' });

    component.salvar();

    const campo = Object.entries(component.form.controls).find(([, c]) => c.errors?.['servidor']);
    expect(campo?.[0]).toBe('nome');
    expect(campo?.[1].errors?.['servidor']).toBe('Já existe um grupo com este nome.');
    expect(component.erros()).toEqual([]);
  });
});
