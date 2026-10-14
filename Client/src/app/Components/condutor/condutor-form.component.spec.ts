import { of, throwError } from 'rxjs';
import { montarFormCrud } from '../../core/testing/crud-form-harness.spec';
import { ClientesService } from '../clientes/clientes.service';
import { CondutorFormComponent } from './condutor-form.component';
import { CondutorDto } from './condutor.model';
import { CondutoresService } from './condutores.service';

describe('CondutorFormComponent — cadastro e edição', () => {
  let svc: jasmine.SpyObj<CondutoresService>;

  const valido = { nome: 'Ana Souza', cpf: '529.982.247-25', cnh: '12345678900', validadeCnh: '2030-01-01', telefone: '(11) 99999-0000', email: 'ana@exemplo.com', clienteId: 'cl1' };

  const montar = (id: string | null = null) => {
    svc = jasmine.createSpyObj('CondutoresService', ['obter', 'criar', 'atualizar']);
    svc.obter.and.returnValue(of({ id: 'c1', ...valido, validadeCnh: '2030-01-01T00:00:00Z' } as unknown as CondutorDto));
    svc.criar.and.returnValue(of({} as CondutorDto));
    svc.atualizar.and.returnValue(of({} as CondutorDto));
    return montarFormCrud(CondutorFormComponent, {
      id,
      providers: [
        { provide: CondutoresService, useValue: svc },
        { provide: ClientesService, useValue: { listar: () => of([{ id: 'cl1', nome: 'Cliente' }]) } }
      ]
    });
  };

  it('carrega os clientes para escolher o vinculado', () => {
    const { component } = montar();

    expect(component.clientes().length).toBe(1);
  });

  it('formulário vazio não envia', () => {
    const { component, notificacao } = montar();

    component.salvar();

    expect(svc.criar).not.toHaveBeenCalled();
    expect(notificacao.aviso).toHaveBeenCalledWith('Verifique os campos destacados.');
  });

  it('CPF com dígito errado e e-mail inválido são recusados', () => {
    const { component } = montar();
    component.form.patchValue({ ...valido, cpf: '111.111.111-11', email: 'sem-arroba' });

    expect(component.form.controls.cpf.invalid).toBeTrue();
    expect(component.form.controls.email.hasError('email')).toBeTrue();
  });

  it('cliente é obrigatório', () => {
    const { component } = montar();
    component.form.patchValue({ ...valido, clienteId: '' });

    expect(component.form.controls.clienteId.hasError('required')).toBeTrue();
  });

  it('cria e volta para a lista', () => {
    const { component, router, notificacao } = montar();
    component.form.patchValue(valido);

    component.salvar();

    expect(svc.criar).toHaveBeenCalled();
    expect(svc.criar.calls.mostRecent().args[0].nome).toBe('Ana Souza');
    expect(notificacao.sucesso).toHaveBeenCalledWith('Condutor criado');
    expect(router.navigate).toHaveBeenCalledWith(['/condutores']);
  });

  it('edita: carrega e atualiza pelo id', () => {
    const { component } = montar('c1');

    expect(component.editMode()).toBeTrue();
    expect(component.form.controls.nome.value).toBe('Ana Souza');

    component.salvar();

    expect(svc.atualizar.calls.mostRecent().args[0]).toBe('c1');
  });

  it('erro do servidor aparece no formulário', () => {
    const { component } = montar();
    svc.criar.and.returnValue(throwError(() => ({ status: 409, error: ['CPF já cadastrado.'] })));
    component.form.patchValue(valido);

    component.salvar();

    const campo = Object.entries(component.form.controls).find(([, c]) => c.errors?.['servidor']);
    expect(campo?.[0]).toBe('cpf');
    expect(campo?.[1].errors?.['servidor']).toBe('CPF já cadastrado.');
    expect(component.erros()).toEqual([]);
  });
});
