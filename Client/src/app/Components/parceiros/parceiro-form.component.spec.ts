import { of, throwError } from 'rxjs';
import { montarFormCrud } from '../../core/testing/crud-form-harness.spec';
import { ParceiroFormComponent } from './parceiro-form.component';
import { ParceiroDto } from './parceiro.model';
import { ParceirosService } from './parceiros.service';

describe('ParceiroFormComponent — cadastro e edição', () => {
  let svc: jasmine.SpyObj<ParceirosService>;

  const valido = { nome: 'Posto Central', cnpj: '11.222.333/0001-81', categoria: 'Combustível', ativo: true };

  const montar = (id: string | null = null) => {
    svc = jasmine.createSpyObj('ParceirosService', ['obter', 'criar', 'atualizar']);
    svc.obter.and.returnValue(of({ id: 'p1', ...valido } as unknown as ParceiroDto));
    svc.criar.and.returnValue(of({} as ParceiroDto));
    svc.atualizar.and.returnValue(of({} as ParceiroDto));
    return montarFormCrud(ParceiroFormComponent, { id, providers: [{ provide: ParceirosService, useValue: svc }] });
  };

  it('formulário vazio não envia', () => {
    const { component, notificacao } = montar();

    component.salvar();

    expect(svc.criar).not.toHaveBeenCalled();
    expect(notificacao.aviso).toHaveBeenCalled();
  });

  it('CNPJ com dígito verificador errado é recusado; um válido passa', () => {
    const { component } = montar();

    component.form.patchValue({ ...valido, cnpj: '11.222.333/0001-82' });
    expect(component.form.controls.cnpj.invalid).toBeTrue();

    component.form.patchValue({ cnpj: '11.222.333/0001-81' });
    expect(component.form.valid).toBeTrue();
  });

  it('cria um parceiro ativo por padrão', () => {
    const { component, router, notificacao } = montar();
    expect(component.form.controls.ativo.value).toBeTrue();
    component.form.patchValue(valido);

    component.salvar();

    expect(svc.criar.calls.mostRecent().args[0].nome).toBe('Posto Central');
    expect(notificacao.sucesso).toHaveBeenCalledWith('Parceiro criado');
    expect(router.navigate).toHaveBeenCalledWith(['/parceiros']);
  });

  it('edita pelo id', () => {
    const { component } = montar('p1');

    expect(component.editMode()).toBeTrue();
    component.salvar();

    expect(svc.atualizar.calls.mostRecent().args[0]).toBe('p1');
  });

  it('erro do servidor aparece no formulário', () => {
    const { component } = montar();
    svc.criar.and.returnValue(throwError(() => ({ status: 409, error: ['CNPJ já cadastrado.'] })));
    component.form.patchValue(valido);

    component.salvar();

    const campo = Object.entries(component.form.controls).find(([, c]) => c.errors?.['servidor']);
    expect(campo?.[0]).toBe('cnpj');
    expect(campo?.[1].errors?.['servidor']).toBe('CNPJ já cadastrado.');
    expect(component.erros()).toEqual([]);
  });
});
