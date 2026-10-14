import { of, throwError } from 'rxjs';
import { montarFormCrud } from '../../core/testing/crud-form-harness.spec';
import { TaxaServicoFormComponent } from './taxa-servico-form.component';
import { TaxaServicoDto } from './taxa-servico.model';
import { TaxasServicosService } from './taxas-servicos.service';
import { violacoesNosDoisTemas } from '../../core/testing/acessibilidade';

describe('TaxaServicoFormComponent — cadastro e edição', () => {
  let svc: jasmine.SpyObj<TaxasServicosService>;

  const montar = (id: string | null = null) => {
    svc = jasmine.createSpyObj('TaxasServicosService', ['obter', 'criar', 'atualizar']);
    svc.obter.and.returnValue(of({ id: 't1', nome: 'GPS', preco: 20, tipoCalculo: 'Fixo' } as unknown as TaxaServicoDto));
    svc.criar.and.returnValue(of({} as TaxaServicoDto));
    svc.atualizar.and.returnValue(of({} as TaxaServicoDto));
    return montarFormCrud(TaxaServicoFormComponent, { id, providers: [{ provide: TaxasServicosService, useValue: svc }] });
  };

  it('nome, preço e tipo de cálculo são obrigatórios; preço não pode ser negativo', () => {
    const { component } = montar();

    expect(component.form.controls.nome.hasError('required')).toBeTrue();
    expect(component.form.controls.tipoCalculo.hasError('required')).toBeTrue();
    component.form.patchValue({ preco: -1 });
    expect(component.form.controls.preco.hasError('min')).toBeTrue();
  });

  it('formulário vazio não envia', () => {
    const { component, notificacao } = montar();

    component.salvar();

    expect(svc.criar).not.toHaveBeenCalled();
    expect(notificacao.aviso).toHaveBeenCalled();
  });

  it('cria (no feminino: "criada") e volta para a lista', () => {
    const { component, router, notificacao } = montar();
    component.form.patchValue({ nome: 'GPS', preco: 20, tipoCalculo: 'Fixo' });

    component.salvar();

    expect(svc.criar.calls.mostRecent().args[0].preco).toBe(20);
    expect(notificacao.sucesso).toHaveBeenCalledWith('Taxa ou serviço criada');
    expect(router.navigate).toHaveBeenCalledWith(['/taxas-servicos']);
  });

  it('edita pelo id (no feminino: "atualizada")', () => {
    const { component, notificacao } = montar('t1');

    component.salvar();

    expect(svc.atualizar.calls.mostRecent().args[0]).toBe('t1');
    expect(notificacao.sucesso).toHaveBeenCalledWith('Taxa ou serviço atualizada');
  });

  it('erro do servidor aparece no formulário', () => {
    const { component } = montar();
    svc.criar.and.returnValue(throwError(() => ({ status: 400, error: ['Tipo de cálculo inválido.'] })));
    component.form.patchValue({ nome: 'GPS', preco: 20, tipoCalculo: 'X' });

    component.salvar();

    const campo = Object.entries(component.form.controls).find(([, c]) => c.errors?.['servidor']);
    expect(campo?.[0]).toBe('tipoCalculo');
    expect(campo?.[1].errors?.['servidor']).toBe('Tipo de cálculo inválido.');
    expect(component.erros()).toEqual([]);
  });

  it('acessibilidade (axe-core, WCAG 2.2 AA): sem violações nos temas claro e escuro', async () => {
    const { fixture } = montar();
    expect(await violacoesNosDoisTemas(fixture.nativeElement)).toEqual([]);
  });
});
