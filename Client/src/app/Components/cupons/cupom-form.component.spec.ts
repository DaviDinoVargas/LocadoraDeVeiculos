import { of, throwError } from 'rxjs';
import { montarFormCrud } from '../../core/testing/crud-form-harness.spec';
import { ParceirosService } from '../parceiros/parceiros.service';
import { CupomFormComponent } from './cupom-form.component';
import { CupomDto } from './cupom.model';
import { CuponsService } from './cupons.service';

describe('CupomFormComponent — cadastro e edição', () => {
  let svc: jasmine.SpyObj<CuponsService>;

  const valido = { codigo: 'NATAL10', descricao: 'Natal', tipoDesconto: 'Percentual', valorDesconto: 10, validoAte: '2026-12-31', limiteUsos: 5, parceiroId: '', ativo: true };

  const montar = (id: string | null = null) => {
    svc = jasmine.createSpyObj('CuponsService', ['obter', 'criar', 'atualizar']);
    svc.obter.and.returnValue(of({ id: 'c1', ...valido, validoAte: '2026-12-31T00:00:00Z', parceiroId: null } as unknown as CupomDto));
    svc.criar.and.returnValue(of({} as CupomDto));
    svc.atualizar.and.returnValue(of({} as CupomDto));
    return montarFormCrud(CupomFormComponent, {
      id,
      providers: [
        { provide: CuponsService, useValue: svc },
        { provide: ParceirosService, useValue: { listar: () => of([{ id: 'p1', nome: 'Parceiro', cnpj: '', categoria: '', ativo: true }]) } }
      ]
    });
  };

  it('carrega a lista de parceiros para o campo opcional', () => {
    const { component } = montar();

    expect(component.parceiros().length).toBe(1);
  });

  it('formulário vazio não envia: avisa e marca os campos', () => {
    const { component, notificacao } = montar();

    component.salvar();

    expect(svc.criar).not.toHaveBeenCalled();
    expect(notificacao.aviso).toHaveBeenCalledWith('Verifique os campos destacados.');
    expect(component.form.controls.codigo.touched).toBeTrue();
  });

  it('limite de usos precisa ser de pelo menos 1 e o valor não pode ser negativo', () => {
    const { component } = montar();
    component.form.patchValue({ ...valido, limiteUsos: 0, valorDesconto: -1 });

    expect(component.form.controls.limiteUsos.hasError('min')).toBeTrue();
    expect(component.form.controls.valorDesconto.hasError('min')).toBeTrue();
    expect(component.form.valid).toBeFalse();
  });

  it('cria: parceiro em branco vai como null, avisa e volta para a lista', () => {
    const { component, router, notificacao } = montar();
    component.form.patchValue(valido);

    component.salvar();

    expect(svc.criar.calls.mostRecent().args[0].parceiroId).toBeNull();
    expect(svc.criar.calls.mostRecent().args[0].codigo).toBe('NATAL10');
    expect(notificacao.sucesso).toHaveBeenCalledWith('Cupom criado');
    expect(router.navigate).toHaveBeenCalledWith(['/cupons']);
  });

  it('edita: carrega o cupom (data sem hora) e atualiza pelo id', () => {
    const { component, notificacao } = montar('c1');

    expect(svc.obter).toHaveBeenCalledWith('c1');
    expect(component.editMode()).toBeTrue();
    expect(component.form.controls.validoAte.value).toBe('2026-12-31');

    component.salvar();

    expect(svc.atualizar.calls.mostRecent().args[0]).toBe('c1');
    expect(notificacao.sucesso).toHaveBeenCalledWith('Cupom atualizado');
  });

  it('erro do servidor aparece no formulário', () => {
    const { component, router } = montar();
    svc.criar.and.returnValue(throwError(() => ({ status: 409, error: ['Já existe um cupom com este código.'] })));
    component.form.patchValue(valido);

    component.salvar();

    expect(component.erros()).toEqual(['Já existe um cupom com este código.']);
    expect(router.navigate).not.toHaveBeenCalled();
    expect(component.salvando()).toBeFalse();
  });

  it('cupom que não carrega volta para a lista', () => {
    svc = jasmine.createSpyObj('CuponsService', ['obter', 'criar', 'atualizar']);
    svc.obter.and.returnValue(throwError(() => ({ status: 404 })));
    const { router } = montarFormCrud(CupomFormComponent, {
      id: 'x',
      providers: [{ provide: CuponsService, useValue: svc }, { provide: ParceirosService, useValue: { listar: () => of([]) } }]
    });

    expect(router.navigate).toHaveBeenCalledWith(['/cupons']);
  });
});
