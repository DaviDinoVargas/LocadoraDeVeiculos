import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { ConfirmacaoService } from '../../core/confirmacao.service';
import { NotificacaoService } from '../../core/notificacao.service';
import { criarNotificacaoFalsa } from '../../core/testing/notificacao-fake.spec';
import { ContaComponent } from './conta.component';
import { violacoesNosDoisTemas } from '../../core/testing/acessibilidade';

describe('ContaComponent — senha, verificação em duas etapas e encerrar conta', () => {
  let auth: jasmine.SpyObj<AuthService>;
  let confirmacao: jasmine.SpyObj<ConfirmacaoService>;
  let router: Router;

  const montar = (cargo: 'Empresa' | 'Funcionario', habilitado = false) => {
    auth = jasmine.createSpyObj('AuthService', [
      'getUsuario', 'alterarSenha', 'statusDoisFatores', 'iniciarDoisFatores', 'ativarDoisFatores', 'desativarDoisFatores', 'encerrarConta'
    ]);
    auth.getUsuario.and.returnValue({ id: '1', nomeCompleto: 'Teste', email: 't@t.com', cargo });
    auth.statusDoisFatores.and.returnValue(of({ habilitado, codigosDeRecuperacaoRestantes: habilitado ? 10 : 0 }));
    confirmacao = jasmine.createSpyObj('ConfirmacaoService', ['confirmar']);

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: ConfirmacaoService, useValue: confirmacao },
        { provide: NotificacaoService, useValue: criarNotificacaoFalsa() }
      ]
    });
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    const fixture = TestBed.createComponent(ContaComponent);
    fixture.detectChanges();
    return fixture;
  };

  describe('trocar a senha (S27)', () => {
    it('senha nova fora da política ou confirmação diferente não chama a API', () => {
      const f = montar('Empresa');
      f.componentInstance.formSenha.patchValue({ atual: 'Atual@12345', nova: 'fraca', confirmar: 'fraca' });
      f.componentInstance.trocarSenha();
      f.componentInstance.formSenha.patchValue({ nova: 'NovaSenha9', confirmar: 'Outra' });
      f.componentInstance.trocarSenha();

      expect(auth.alterarSenha).not.toHaveBeenCalled();
    });

    it('troca com sucesso e leva ao login (o servidor derrubou todas as sessões)', () => {
      const f = montar('Empresa');
      auth.alterarSenha.and.returnValue(of(undefined));
      f.componentInstance.formSenha.patchValue({ atual: 'Atual@12345', nova: 'NovaSenha9', confirmar: 'NovaSenha9' });

      f.componentInstance.trocarSenha();

      expect(auth.alterarSenha).toHaveBeenCalledOnceWith('Atual@12345', 'NovaSenha9', 'NovaSenha9');
      expect(router.navigate).toHaveBeenCalledWith(['/login']);
    });

    it('mostra a mensagem específica do servidor (senha atual incorreta)', () => {
      const f = montar('Empresa');
      auth.alterarSenha.and.returnValue(throwError(() => ({ status: 400, error: ['Requisição inválida', 'A senha atual está incorreta.'] })));
      f.componentInstance.formSenha.patchValue({ atual: 'Errada@1234', nova: 'NovaSenha9', confirmar: 'NovaSenha9' });

      f.componentInstance.trocarSenha();

      expect(f.componentInstance.erroSenha()).toBe('A senha atual está incorreta.');
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });

  describe('verificação em duas etapas (S18)', () => {
    it('sem 2FA oferece configurar; configurar mostra a chave e ativar mostra os códigos de recuperação', () => {
      const f = montar('Empresa', false);
      auth.iniciarDoisFatores.and.returnValue(of({ chave: 'abcd efgh ijkl', uri: 'otpauth://totp/x' }));
      auth.ativarDoisFatores.and.returnValue(of({ codigos: ['AAAAA-BBBBB', 'CCCCC-DDDDD'] }));

      f.componentInstance.configurarDoisFatores();
      f.detectChanges();
      expect(f.nativeElement.textContent).toContain('abcd efgh ijkl');

      f.componentInstance.formAtivar.patchValue({ codigo: '123456' });
      auth.statusDoisFatores.and.returnValue(of({ habilitado: true, codigosDeRecuperacaoRestantes: 2 }));
      f.componentInstance.ativarDoisFatores();
      f.detectChanges();

      expect(auth.ativarDoisFatores).toHaveBeenCalledOnceWith('123456');
      expect(f.componentInstance.codigosDeRecuperacao()).toEqual(['AAAAA-BBBBB', 'CCCCC-DDDDD']);
      expect(f.nativeElement.textContent).toContain('Guarde estes códigos de recuperação');
      expect(f.componentInstance.configuracao()).toBeNull();
    });

    it('código errado na ativação mostra o erro e não ativa', () => {
      const f = montar('Empresa', false);
      auth.iniciarDoisFatores.and.returnValue(of({ chave: 'k', uri: 'u' }));
      auth.ativarDoisFatores.and.returnValue(throwError(() => ({ status: 400, error: ['Requisição inválida', 'Código incorreto. Confira o aplicativo e tente de novo.'] })));
      f.componentInstance.configurarDoisFatores();
      f.componentInstance.formAtivar.patchValue({ codigo: '000000' });

      f.componentInstance.ativarDoisFatores();

      expect(f.componentInstance.erro2fa()).toContain('Código incorreto');
      expect(f.componentInstance.codigosDeRecuperacao()).toEqual([]);
    });

    it('com 2FA ativo mostra o status e exige senha e código para desativar', () => {
      const f = montar('Empresa', true);
      expect(f.nativeElement.textContent).toContain('10 código(s) de recuperação restante(s)');

      f.componentInstance.desativarDoisFatores();
      expect(auth.desativarDoisFatores).not.toHaveBeenCalled();

      auth.desativarDoisFatores.and.returnValue(of(undefined));
      f.componentInstance.formDesativar.patchValue({ senha: 'Senha@12345', codigo: '123456' });
      f.componentInstance.desativarDoisFatores();

      expect(auth.desativarDoisFatores).toHaveBeenCalledOnceWith('Senha@12345', '123456');
    });
  });

  describe('encerrar a conta (S1)', () => {
    it('só a Empresa vê a seção de encerrar conta', () => {
      expect(montar('Empresa').nativeElement.textContent).toContain('Encerrar a conta da empresa');
      TestBed.resetTestingModule();
      expect(montar('Funcionario').nativeElement.textContent).not.toContain('Encerrar a conta da empresa');
    });

    it('cancelar o diálogo não encerra nada', () => {
      const f = montar('Empresa');
      confirmacao.confirmar.and.returnValue(of(false));
      f.componentInstance.formEncerrar.patchValue({ senha: 'Senha@12345' });

      f.componentInstance.encerrarConta();

      expect(confirmacao.confirmar).toHaveBeenCalledWith(jasmine.objectContaining({ destrutivo: true }));
      expect(auth.encerrarConta).not.toHaveBeenCalled();
    });

    it('confirmando, encerra com a senha e leva ao login', () => {
      const f = montar('Empresa');
      confirmacao.confirmar.and.returnValue(of(true));
      auth.encerrarConta.and.returnValue(of(undefined));
      f.componentInstance.formEncerrar.patchValue({ senha: 'Senha@12345' });

      f.componentInstance.encerrarConta();

      expect(auth.encerrarConta).toHaveBeenCalledOnceWith('Senha@12345');
      expect(router.navigate).toHaveBeenCalledWith(['/login']);
    });

    it('senha errada mostra o erro e continua logado', () => {
      const f = montar('Empresa');
      confirmacao.confirmar.and.returnValue(of(true));
      auth.encerrarConta.and.returnValue(throwError(() => ({ status: 400, error: ['Requisição inválida', 'A senha está incorreta.'] })));
      f.componentInstance.formEncerrar.patchValue({ senha: 'Errada@1234' });

      f.componentInstance.encerrarConta();

      expect(f.componentInstance.erroEncerrar()).toBe('A senha está incorreta.');
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('sem a senha preenchida nem abre o diálogo', () => {
      const f = montar('Empresa');

      f.componentInstance.encerrarConta();

      expect(confirmacao.confirmar).not.toHaveBeenCalled();
    });
  });

  it('acessibilidade (axe-core, WCAG 2.2 AA): sem violações nos temas claro e escuro', async () => {
    const fixture = montar('Empresa');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(await violacoesNosDoisTemas(fixture.nativeElement)).toEqual([]);
  });
});
