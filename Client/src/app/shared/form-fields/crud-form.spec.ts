import { Component, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { NotificacaoService } from '../../core/notificacao.service';
import { criarNotificacaoFalsa } from '../../core/testing/notificacao-fake.spec';
import { crudForm } from './crud-form';

interface Dto {
  nome: string;
}

@Component({ imports: [ReactiveFormsModule], template: '' })
class HostComponent {
  obter = jasmine.createSpy('obter').and.returnValue(of({ nome: 'Carregado' }));
  criar = jasmine.createSpy('criar').and.returnValue(of({}));
  atualizar = jasmine.createSpy('atualizar').and.returnValue(of({}));

  readonly form = inject(NonNullableFormBuilder).group({ nome: ['', Validators.required] });
  readonly crud = crudForm<Dto, typeof this.form.controls>({
    form: this.form,
    obter: id => this.obter(id),
    criar: p => this.criar(p),
    atualizar: (id, p) => this.atualizar(id, p),
    rotaLista: '/lista',
    singular: 'grupo de veículo'
  });
}

describe('crudForm — cadastro e edição com um único componente', () => {
  let params: BehaviorSubject<ReturnType<typeof convertToParamMap>>;
  let notificacao: jasmine.SpyObj<NotificacaoService>;
  let router: Router;

  const criar = (id: string | null = null) => {
    params = new BehaviorSubject(convertToParamMap(id ? { id } : {}));
    notificacao = criarNotificacaoFalsa();
    TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [
        provideRouter([]),
        { provide: NotificacaoService, useValue: notificacao },
        { provide: ActivatedRoute, useValue: { paramMap: params, snapshot: { paramMap: params.value } } }
      ]
    });
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    return TestBed.createComponent(HostComponent).componentInstance;
  };

  it('sem :id é cadastro: não carrega nada e salva com criar()', () => {
    const host = criar();
    host.form.setValue({ nome: 'Novo' });

    host.crud.salvar();

    expect(host.crud.editMode()).toBeFalse();
    expect(host.obter).not.toHaveBeenCalled();
    expect(host.criar).toHaveBeenCalledOnceWith({ nome: 'Novo' });
    expect(notificacao.sucesso).toHaveBeenCalledWith('Grupo de veículo criado');
    expect(router.navigate).toHaveBeenCalledWith(['/lista']);
  });

  it('com :id é edição: carrega o registro, deixa o formulário "limpo" e salva com atualizar()', () => {
    const host = criar('abc');

    expect(host.crud.editMode()).toBeTrue();
    expect(host.obter).toHaveBeenCalledOnceWith('abc');
    expect(host.form.value.nome).toBe('Carregado');
    expect(host.form.dirty).toBeFalse();

    host.form.setValue({ nome: 'Editado' });
    host.form.markAsDirty();
    host.crud.salvar();

    expect(host.atualizar).toHaveBeenCalledOnceWith('abc', { nome: 'Editado' });
    expect(host.form.dirty).toBeFalse(); // salvar e navegar não deve pedir confirmação
    expect(notificacao.sucesso).toHaveBeenCalledWith('Grupo de veículo atualizado');
  });

  it('formulário inválido não envia: marca os campos, avisa e não chama a API', () => {
    const host = criar();

    host.crud.salvar();

    expect(host.criar).not.toHaveBeenCalled();
    expect(host.form.controls.nome.touched).toBeTrue();
    expect(notificacao.aviso).toHaveBeenCalled();
  });

  it('erro do servidor sem campo definido mostra a mensagem real no alerta e mantém a tela', () => {
    const host = criar();
    host.criar.and.returnValue(throwError(() => ({ status: 409, error: ['O limite de grupos da empresa foi atingido.'] })));
    host.form.setValue({ nome: 'Repetido' });

    host.crud.salvar();

    expect(host.crud.erros()).toEqual(['O limite de grupos da empresa foi atingido.']);
    expect(host.form.controls.nome.errors).toBeNull();
    expect(host.crud.salvando()).toBeFalse();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('erro do servidor que cita um campo vai para o próprio campo, com aviso, e some ao editar', () => {
    const host = criar();
    host.criar.and.returnValue(throwError(() => ({ status: 409, error: ['Já existe um grupo com esse nome.', 'Outro problema qualquer.'] })));
    host.form.setValue({ nome: 'Repetido' });

    host.crud.salvar();

    expect(host.form.controls.nome.errors).toEqual({ servidor: 'Já existe um grupo com esse nome.' });
    expect(host.form.controls.nome.touched).toBeTrue();
    expect(host.crud.erros()).toEqual(['Outro problema qualquer.']);
    expect(notificacao.aviso).toHaveBeenCalledWith('Verifique os campos destacados.');

    host.form.controls.nome.setValue('Outro nome');

    expect(host.form.controls.nome.errors).toBeNull();
  });

  it('falha ao carregar a edição volta para a lista', () => {
    const host = criar('x');
    host.obter.and.returnValue(throwError(() => ({ status: 404 })));

    params.next(convertToParamMap({ id: 'y' })); // abre outro registro, que não existe

    expect(router.navigate).toHaveBeenCalledWith(['/lista']);
    expect(host.crud.carregando()).toBeFalse();
  });
});
