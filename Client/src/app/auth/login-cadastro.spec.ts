import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { NotificacaoService } from '../core/notificacao.service';
import { criarNotificacaoFalsa } from '../core/testing/notificacao-fake.spec';
import { WebcamCaptureService } from '../shared/webcam-capture.service';
import { AuthService } from './auth.service';
import { LoginComponent } from './login.component';
import { RegisterComponent } from './register.component';

describe('Login e cadastro — sem Angular Material, com formulário à direita', () => {
  let auth: jasmine.SpyObj<AuthService>;
  let notificacao: jasmine.SpyObj<NotificacaoService>;
  let router: Router;

  const configurar = () => {
    auth = jasmine.createSpyObj('AuthService', ['autenticar', 'registrar', 'isLoggedIn', 'entrarComRosto']);
    auth.isLoggedIn.and.returnValue(false);
    notificacao = criarNotificacaoFalsa();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: NotificacaoService, useValue: notificacao },
        { provide: WebcamCaptureService, useValue: jasmine.createSpyObj('WebcamCaptureService', ['ligar', 'desligar', 'capturarFrameBase64']) }
      ]
    });
    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);
    spyOn(router, 'navigate').and.resolveTo(true);
  };

  describe('LoginComponent', () => {
    const criar = () => {
      const fixture = TestBed.createComponent(LoginComponent);
      fixture.detectChanges();
      return fixture;
    };

    it('mostra rótulo visível, autocomplete certo e uma única ação primária larga', () => {
      configurar();
      const el = criar().nativeElement as HTMLElement;

      expect(el.querySelector('input[type="email"]')?.getAttribute('autocomplete')).toBe('email');
      expect(el.querySelector('input[type="password"]')?.getAttribute('autocomplete')).toBe('current-password');
      expect(el.textContent).toContain('E-mail');
      expect(el.querySelectorAll('button[type="submit"]').length).toBe(1);
    });

    it('a moldura tem a altura da janela e não rola no desktop (o painel da marca não estoura a tela)', () => {
      configurar();
      const raiz = (criar().nativeElement as HTMLElement).querySelector('app-auth-shell > div') as HTMLElement;

      expect(raiz.className).toContain('h-dvh');
      expect(raiz.className).toContain('overflow-hidden');
    });

    it('formulário inválido não chama a API e mostra os erros dos campos', () => {
      configurar();
      const fixture = criar();

      fixture.componentInstance.entrar();
      fixture.detectChanges();

      expect(auth.autenticar).not.toHaveBeenCalled();
      expect(fixture.nativeElement.textContent).toContain('Campo obrigatório');
    });

    it('credenciais erradas mostram "E-mail ou senha incorretos." sem revelar se o e-mail existe', () => {
      configurar();
      auth.autenticar.and.returnValue(throwError(() => ({ status: 401 })));
      const fixture = criar();
      fixture.componentInstance.form.setValue({ email: 'a@b.com', senha: 'errada' });

      fixture.componentInstance.entrar();
      fixture.detectChanges();

      expect(fixture.componentInstance.erro()).toBe('E-mail ou senha incorretos.');
      expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent).toContain('E-mail ou senha incorretos.');
      expect(fixture.componentInstance.carregando()).toBeFalse();
    });

    it('login correto avisa e vai para o destino', () => {
      configurar();
      auth.autenticar.and.returnValue(of({ accessToken: 't', expires: '' }));
      const fixture = criar();
      fixture.componentInstance.form.setValue({ email: 'a@b.com', senha: '123456' });

      fixture.componentInstance.entrar();

      expect(notificacao.sucesso).toHaveBeenCalled();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/home');
    });

    it('o botão do olho alterna mostrar/ocultar a senha com aria-pressed', () => {
      configurar();
      const fixture = criar();
      const olho = fixture.nativeElement.querySelector('button[aria-pressed]') as HTMLButtonElement;

      olho.click();
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('input[formControlName="senha"]').type).toBe('text');
      expect(olho.getAttribute('aria-pressed')).toBe('true');
    });
  });

  describe('RegisterComponent', () => {
    const criar = () => {
      const fixture = TestBed.createComponent(RegisterComponent);
      fixture.detectChanges();
      return fixture;
    };

    it('senhas diferentes: erro embaixo do campo de confirmação e não envia', () => {
      configurar();
      const fixture = criar();
      fixture.componentInstance.form.setValue({ nomeCompleto: 'Ana', email: 'a@b.com', senha: '123456', confirmarSenha: '654321' });

      fixture.componentInstance.registrar();
      fixture.detectChanges();

      expect(auth.registrar).not.toHaveBeenCalled();
      expect(fixture.nativeElement.textContent).toContain('As senhas não coincidem');
    });

    it('cadastro válido chama a API e vai para a home', () => {
      configurar();
      auth.registrar.and.returnValue(of({ accessToken: 't', expires: '' }));
      const fixture = criar();
      fixture.componentInstance.form.setValue({ nomeCompleto: 'Ana', email: 'a@b.com', senha: '123456', confirmarSenha: '123456' });

      fixture.componentInstance.registrar();

      expect(auth.registrar).toHaveBeenCalledOnceWith('Ana', 'a@b.com', '123456', '123456');
      expect(router.navigate).toHaveBeenCalledWith(['/home']);
    });

    it('erro do servidor aparece no alerta do formulário com a mensagem real', () => {
      configurar();
      auth.registrar.and.returnValue(throwError(() => ({ status: 409, error: ['E-mail já cadastrado.'] })));
      const fixture = criar();
      fixture.componentInstance.form.setValue({ nomeCompleto: 'Ana', email: 'a@b.com', senha: '123456', confirmarSenha: '123456' });

      fixture.componentInstance.registrar();

      expect(fixture.componentInstance.erro()).toBe('E-mail já cadastrado.');
    });
  });
});
