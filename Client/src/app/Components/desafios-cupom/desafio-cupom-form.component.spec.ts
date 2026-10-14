import { of, throwError } from 'rxjs';
import { montarFormCrud } from '../../core/testing/crud-form-harness.spec';
import { CuponsService } from '../cupons/cupons.service';
import { DesafioCupomFormComponent } from './desafio-cupom-form.component';
import { DesafioCupomDto } from './desafio-cupom.model';
import { DesafiosCupomService } from './desafios-cupom.service';
import { violacoesNosDoisTemas } from '../../core/testing/acessibilidade';

describe('DesafioCupomFormComponent — cadastro e edição', () => {
  let svc: jasmine.SpyObj<DesafiosCupomService>;

  const valido = { nome: 'Cliente fiel', descricao: '3 aluguéis em 60 dias', metaQuantidadeAlugueis: 3, periodoDias: 60, cupomRecompensaId: 'cup1', ativo: true };

  const montar = (id: string | null = null) => {
    svc = jasmine.createSpyObj('DesafiosCupomService', ['obter', 'criar', 'atualizar']);
    svc.obter.and.returnValue(of({ id: 'd1', ...valido } as unknown as DesafioCupomDto));
    svc.criar.and.returnValue(of({} as DesafioCupomDto));
    svc.atualizar.and.returnValue(of({} as DesafioCupomDto));
    return montarFormCrud(DesafioCupomFormComponent, {
      id,
      providers: [
        { provide: DesafiosCupomService, useValue: svc },
        { provide: CuponsService, useValue: { listar: () => of([{ id: 'cup1', codigo: 'FIEL10' }]) } }
      ]
    });
  };

  it('carrega os cupons que podem ser a recompensa', () => {
    const { component } = montar();

    expect(component.cupons().length).toBe(1);
  });

  it('meta e período precisam ser de pelo menos 1; o cupom de recompensa é obrigatório', () => {
    const { component } = montar();
    component.form.patchValue({ ...valido, metaQuantidadeAlugueis: 0, periodoDias: 0, cupomRecompensaId: '' });

    expect(component.form.controls.metaQuantidadeAlugueis.hasError('min')).toBeTrue();
    expect(component.form.controls.periodoDias.hasError('min')).toBeTrue();
    expect(component.form.controls.cupomRecompensaId.hasError('required')).toBeTrue();
  });

  it('formulário vazio não envia', () => {
    const { component, notificacao } = montar();

    component.salvar();

    expect(svc.criar).not.toHaveBeenCalled();
    expect(notificacao.aviso).toHaveBeenCalled();
  });

  it('cria um desafio e volta para a lista', () => {
    const { component, router, notificacao } = montar();
    component.form.patchValue(valido);

    component.salvar();

    expect(svc.criar.calls.mostRecent().args[0].metaQuantidadeAlugueis).toBe(3);
    expect(notificacao.sucesso).toHaveBeenCalledWith('Desafio criado');
    expect(router.navigate).toHaveBeenCalledWith(['/desafios-cupom']);
  });

  it('edita pelo id', () => {
    const { component } = montar('d1');

    component.salvar();

    expect(svc.atualizar.calls.mostRecent().args[0]).toBe('d1');
  });

  it('erro do servidor aparece no formulário', () => {
    const { component } = montar();
    svc.criar.and.returnValue(throwError(() => ({ status: 409, error: ['Já existe um desafio com este nome.'] })));
    component.form.patchValue(valido);

    component.salvar();

    const campo = Object.entries(component.form.controls).find(([, c]) => c.errors?.['servidor']);
    expect(campo?.[0]).toBe('nome');
    expect(campo?.[1].errors?.['servidor']).toBe('Já existe um desafio com este nome.');
    expect(component.erros()).toEqual([]);
  });

  it('acessibilidade (axe-core, WCAG 2.2 AA): sem violações nos temas claro e escuro', async () => {
    const { fixture } = montar();
    expect(await violacoesNosDoisTemas(fixture.nativeElement)).toEqual([]);
  });
});
