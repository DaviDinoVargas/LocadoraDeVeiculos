import { of, throwError } from 'rxjs';
import { montarFormCrud } from '../../core/testing/crud-form-harness.spec';
import { ClienteDto, ClientePessoaFisicaDto, ClientePessoaJuridicaDto } from './cliente.model';
import { ClientePessoaFisicaFormComponent } from './ClientePessoaFisicaFormComponent';
import { ClientePessoaJuridicaFormComponent } from './ClientePessoaJuridicaFormComponent';
import { ClientesService } from './clientes.service';

const CPF = '529.982.247-25';
const CNPJ = '11.222.333/0001-81';

describe('Formulário de cliente pessoa física', () => {
  let svc: jasmine.SpyObj<ClientesService>;

  const pessoaFisica = (extra: Partial<ClienteDto> = {}): ClienteDto =>
    ({ id: 'c1', nome: 'Maria Souza', telefone: '(11) 99999-0000', tipoCliente: 'PessoaFisica', cpf: '52998224725', rg: '12.345.678-9', cnh: '12345678900',
       validadeCnh: '2030-05-20T00:00:00', email: 'maria@exemplo.com', endereco: 'Rua A, 10', clientePessoaJuridicaId: null, ...extra }) as unknown as ClienteDto;

  const montar = (id: string | null = null, cliente: ClienteDto = pessoaFisica()) => {
    svc = jasmine.createSpyObj('ClientesService', ['obter', 'criarPessoaFisica', 'atualizarPessoaFisica', 'listarPessoasJuridicas']);
    svc.obter.and.returnValue(of(cliente));
    svc.criarPessoaFisica.and.returnValue(of({} as ClientePessoaFisicaDto));
    svc.atualizarPessoaFisica.and.returnValue(of({} as ClientePessoaFisicaDto));
    svc.listarPessoasJuridicas.and.returnValue(of([{ id: 'pj1', nome: 'Frota SA', cnpj: CNPJ } as unknown as ClientePessoaJuridicaDto]));
    return montarFormCrud(ClientePessoaFisicaFormComponent, { id, providers: [{ provide: ClientesService, useValue: svc }] });
  };

  it('oferece as empresas-cliente para vincular a pessoa', () => {
    const { component } = montar();

    expect(svc.listarPessoasJuridicas).toHaveBeenCalledTimes(1);
    expect(component.pessoasJuridicas().map(p => p.nome)).toEqual(['Frota SA']);
  });

  it('só nome, CPF e telefone são obrigatórios; sem eles não envia e marca os campos', () => {
    const { component, notificacao } = montar();

    component.salvar();

    expect(svc.criarPessoaFisica).not.toHaveBeenCalled();
    expect(component.form.controls.nome.touched).toBeTrue();
    expect(component.form.controls.cpf.hasError('required')).toBeTrue();
    expect(component.form.controls.telefone.hasError('required')).toBeTrue();
    expect(notificacao.aviso).toHaveBeenCalled();
  });

  it('CPF com dígito verificador errado não passa (a API também recusa)', () => {
    const { component } = montar();

    component.form.patchValue({ nome: 'Maria', cpf: '123.456.789-00', telefone: '(11) 99999-0000' });
    component.salvar();

    expect(component.form.controls.cpf.invalid).toBeTrue();
    expect(svc.criarPessoaFisica).not.toHaveBeenCalled();
  });

  it('o payload leva só o que foi preenchido, e sem vínculo manda null (a API distingue "sem vínculo")', () => {
    const { component } = montar();

    component.form.patchValue({ nome: 'Maria Souza', cpf: CPF, telefone: '(11) 99999-0000', email: '', rg: '' });
    component.salvar();

    expect(svc.criarPessoaFisica).toHaveBeenCalledOnceWith({ nome: 'Maria Souza', cpf: CPF, telefone: '(11) 99999-0000', clientePessoaJuridicaId: null });
  });

  it('com todos os campos e vínculo, o payload leva tudo', () => {
    const { component } = montar();

    component.form.patchValue({
      nome: 'Maria Souza', cpf: CPF, telefone: '(11) 99999-0000', rg: '12.345.678-9', cnh: '12345678900', validadeCnh: '2030-05-20',
      email: 'maria@exemplo.com', endereco: 'Rua A, 10', clientePessoaJuridicaId: 'pj1'
    });
    component.salvar();

    expect(svc.criarPessoaFisica).toHaveBeenCalledOnceWith({
      nome: 'Maria Souza', cpf: CPF, telefone: '(11) 99999-0000', rg: '12.345.678-9', cnh: '12345678900', validadeCnh: '2030-05-20',
      email: 'maria@exemplo.com', endereco: 'Rua A, 10', clientePessoaJuridicaId: 'pj1'
    });
  });

  it('editar carrega os dados (CPF com máscara, data só com o dia) e salva pelo id', () => {
    const { component, router, notificacao } = montar('c1');

    expect(component.editMode()).toBeTrue();
    expect(component.form.value.cpf).toBe(CPF);
    expect(component.form.value.validadeCnh).toBe('2030-05-20');
    expect(component.form.value.nome).toBe('Maria Souza');

    component.form.patchValue({ nome: 'Maria Souza Lima' });
    component.salvar();

    expect(svc.atualizarPessoaFisica).toHaveBeenCalledOnceWith('c1', jasmine.objectContaining({ nome: 'Maria Souza Lima', cpf: CPF }));
    expect(notificacao.sucesso).toHaveBeenCalledWith('Cliente atualizado');
    expect(router.navigate).toHaveBeenCalledWith(['/clientes']);
  });

  it('abrir para editar um cliente que é pessoa jurídica avisa e volta para a lista (não preenche o formulário errado)', () => {
    const { component, router, notificacao } = montar('c2', { id: 'c2', nome: 'Empresa', tipoCliente: 'PessoaJuridica' } as unknown as ClienteDto);

    expect(notificacao.erro).toHaveBeenCalledWith('Este cliente não é uma pessoa física.');
    expect(router.navigate).toHaveBeenCalledWith(['/clientes']);
    expect(component.form.value.nome).toBe('');
  });

  it('CPF já cadastrado (409 da API) aparece no próprio campo CPF, com foco, e não no alerta do topo', () => {
    const { component, notificacao } = montar();
    svc.criarPessoaFisica.and.returnValue(throwError(() => ({ status: 409, error: ['Já existe um cliente com este CPF.'] })));
    component.form.patchValue({ nome: 'Maria', cpf: CPF, telefone: '(11) 99999-0000' });

    component.salvar();

    expect(component.form.controls.cpf.errors).toEqual({ servidor: 'Já existe um cliente com este CPF.' });
    expect(component.erros()).toEqual([]);
    expect(notificacao.aviso).toHaveBeenCalledWith('Verifique os campos destacados.');
    expect(component.salvando()).toBeFalse();
  });
});

describe('Formulário de cliente pessoa jurídica', () => {
  let svc: jasmine.SpyObj<ClientesService>;

  const pessoaJuridica = (): ClienteDto =>
    ({ id: 'j1', nome: 'Frota SA', telefone: '(11) 3333-4444', tipoCliente: 'PessoaJuridica', cnpj: '11222333000181', nomeFantasia: 'Frota', email: 'f@frota.com', endereco: 'Av. B, 5' }) as unknown as ClienteDto;

  const montar = (id: string | null = null, cliente: ClienteDto = pessoaJuridica()) => {
    svc = jasmine.createSpyObj('ClientesService', ['obter', 'criarPessoaJuridica', 'atualizarPessoaJuridica']);
    svc.obter.and.returnValue(of(cliente));
    svc.criarPessoaJuridica.and.returnValue(of({} as ClientePessoaJuridicaDto));
    svc.atualizarPessoaJuridica.and.returnValue(of({} as ClientePessoaJuridicaDto));
    return montarFormCrud(ClientePessoaJuridicaFormComponent, { id, providers: [{ provide: ClientesService, useValue: svc }] });
  };

  it('CNPJ com dígito verificador errado ou sem preencher não permite salvar', () => {
    const { component } = montar();

    component.form.patchValue({ nome: 'Frota SA', cnpj: '11.111.111/1111-11', telefone: '(11) 3333-4444' });
    component.salvar();
    expect(component.form.controls.cnpj.invalid).toBeTrue();

    component.form.patchValue({ cnpj: '' });
    component.salvar();
    expect(component.form.controls.cnpj.hasError('required')).toBeTrue();
    expect(svc.criarPessoaJuridica).not.toHaveBeenCalled();
  });

  it('cria com o formulário completo exatamente como foi preenchido', () => {
    const { component, router } = montar();

    component.form.patchValue({ nome: 'Frota SA', cnpj: CNPJ, nomeFantasia: 'Frota', telefone: '(11) 3333-4444', email: 'f@frota.com', endereco: 'Av. B, 5' });
    component.salvar();

    expect(svc.criarPessoaJuridica).toHaveBeenCalledOnceWith({ nome: 'Frota SA', cnpj: CNPJ, nomeFantasia: 'Frota', telefone: '(11) 3333-4444', email: 'f@frota.com', endereco: 'Av. B, 5' });
    expect(router.navigate).toHaveBeenCalledWith(['/clientes']);
  });

  it('editar carrega o CNPJ com máscara e salva pelo id', () => {
    const { component, notificacao } = montar('j1');

    expect(component.editMode()).toBeTrue();
    expect(component.form.value.cnpj).toBe(CNPJ);
    expect(component.form.value.nomeFantasia).toBe('Frota');

    component.form.patchValue({ nomeFantasia: 'Frota Nova' });
    component.salvar();

    expect(svc.atualizarPessoaJuridica).toHaveBeenCalledOnceWith('j1', jasmine.objectContaining({ nomeFantasia: 'Frota Nova', cnpj: CNPJ }));
    expect(notificacao.sucesso).toHaveBeenCalledWith('Cliente atualizado');
  });

  it('abrir para editar uma pessoa física avisa e volta para a lista', () => {
    const { router, notificacao } = montar('p1', { id: 'p1', nome: 'Maria', tipoCliente: 'PessoaFisica' } as unknown as ClienteDto);

    expect(notificacao.erro).toHaveBeenCalledWith('Este cliente não é uma pessoa jurídica.');
    expect(router.navigate).toHaveBeenCalledWith(['/clientes']);
  });

  it('CNPJ repetido (409) aparece no campo CNPJ', () => {
    const { component } = montar();
    svc.criarPessoaJuridica.and.returnValue(throwError(() => ({ status: 409, error: ['Já existe um cliente com este CNPJ.'] })));
    component.form.patchValue({ nome: 'Frota SA', cnpj: CNPJ, telefone: '(11) 3333-4444' });

    component.salvar();

    expect(component.form.controls.cnpj.errors).toEqual({ servidor: 'Já existe um cliente com este CNPJ.' });
    expect(component.erros()).toEqual([]);
  });
});
