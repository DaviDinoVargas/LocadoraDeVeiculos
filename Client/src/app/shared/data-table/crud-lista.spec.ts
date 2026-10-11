import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { ConfirmacaoService } from '../../core/confirmacao.service';
import { NotificacaoService } from '../../core/notificacao.service';
import { criarNotificacaoFalsa } from '../../core/testing/notificacao-fake.spec';
import { crudLista } from './crud-lista';

interface Item {
  id: string;
}

@Component({ template: '' })
class HostComponent {
  listar = jasmine.createSpy('listar').and.returnValue(of([{ id: '1' }, { id: '2' }] as Item[]));
  excluir = jasmine.createSpy('excluir').and.returnValue(of(undefined));
  readonly lista = crudLista<Item>({
    listar: () => this.listar() as Observable<Item[]>,
    excluir: id => this.excluir(id) as Observable<unknown>,
    singular: 'grupo de veículo'
  });
}

describe('crudLista — carregar e excluir das listagens', () => {
  let confirmar: jasmine.Spy;
  let notificacao: jasmine.SpyObj<NotificacaoService>;

  const criar = (confirma = true) => {
    confirmar = jasmine.createSpy('confirmar').and.returnValue(of(confirma));
    notificacao = criarNotificacaoFalsa();
    TestBed.configureTestingModule({
      providers: [{ provide: ConfirmacaoService, useValue: { confirmar } }, { provide: NotificacaoService, useValue: notificacao }]
    });
    return TestBed.createComponent(HostComponent).componentInstance;
  };

  it('carrega ao criar e sai do estado "carregando"', () => {
    const host = criar();

    expect(host.lista.dados().length).toBe(2);
    expect(host.lista.estado()).toBe('pronto');
    expect(host.lista.carregando()).toBeFalse();
  });

  it('falha ao carregar vira estado de erro (a tela mostra "Tentar novamente")', () => {
    TestBed.resetTestingModule();
    const host = criar();
    host.listar.and.returnValue(throwError(() => new Error('falhou')));

    host.lista.carregar();

    expect(host.lista.erro()).toBeTrue();
    expect(host.lista.carregando()).toBeFalse();
  });

  it('excluir pede confirmação destrutiva; ao confirmar exclui, avisa e recarrega', () => {
    const host = criar(true);
    host.listar.calls.reset();

    host.lista.excluir('1');

    expect(confirmar).toHaveBeenCalledWith(jasmine.objectContaining({ confirmar: 'Excluir', destrutivo: true, mensagem: 'Deseja realmente excluir este grupo de veículo?' }));
    expect(host.excluir).toHaveBeenCalledOnceWith('1');
    expect(notificacao.sucesso).toHaveBeenCalledWith('Grupo de veículo excluído');
    expect(host.listar).toHaveBeenCalledTimes(1);
  });

  it('cancelar a confirmação não exclui nada', () => {
    const host = criar(false);

    host.lista.excluir('1');

    expect(host.excluir).not.toHaveBeenCalled();
  });

  it('sem id não abre diálogo', () => {
    const host = criar();

    host.lista.excluir(undefined);

    expect(confirmar).not.toHaveBeenCalled();
  });
});
