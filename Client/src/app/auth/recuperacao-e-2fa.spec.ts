import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { NotificacaoService } from '../core/notificacao.service';
import { criarNotificacaoFalsa } from '../core/testing/notificacao-fake.spec';
import { WebcamCaptureService } from '../shared/webcam-capture.service';
import { AuthService } from './auth.service';
import { EsqueciSenhaComponent } from './esqueci-senha.component';
import { LoginComponent } from './login.component';
import { RedefinirSenhaComponent } from './redefinir-senha.component';

describe('Recuperação de senha e segundo fator no login (S18)', () => {
  let auth: jasmine.SpyObj<AuthService>;
  let router: Router;

  const configurar = (fragmento: string | null = null) => {
    auth = jasmine.createSpyObj('AuthService', ['autenticar', 'entrarComRosto', 'isLoggedIn', 'solicitarRecuperacaoDeSenha', 'redefinirSenha']);
    auth.isLoggedIn.and.returnValue(false);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: NotificacaoService, useValue: criarNotificacaoFalsa() },
        { provide: ActivatedRoute, useValue: { snapshot: { fragment: fragmento, queryParams: {} } } },
        { provide: WebcamCaptureService, useValue: jasmine.createSpyObj('WebcamCaptureService', ['ligar', 'desligar', 'capturarFrameBase64']) }
      ]
    });
    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);
    spyOn(router, 'navigate').and.resolveTo(true);
  };

  describe('LoginComponent com verificação em duas etapas', () => {
    it('mostra o link "Esqueci minha senha"', () => {
      configurar();
      const fixture = TestBed.createComponent(LoginComponent);
      fixture.detectChanges();

      const link = (fixture.nativeElement as HTMLElement).querySelector('a[href="/esqueci-senha"]');
      expect(link?.textContent).toContain('Esqueci minha senha');
    });

    it('428 (senha certa, falta o código) mostra o campo do código e repete o pedido com ele', () => {
      configurar();
      const fixture = TestBed.createComponent(LoginComponent);
      fixture.detectChanges();
      const c = fixture.componentInstance;
      c.form.patchValue({ email: 'a@b.com', senha: 'Senha@12345' });

      auth.autenticar.and.returnValue(throwError(() => ({ status: 428 })));
      c.entrar();
      fixture.detectChanges();

      expect(c.pedirCodigo()).toBeTrue();
      expect(fixture.nativeElement.textContent).toContain('Código de verificação');
      expect(auth.autenticar).toHaveBeenCalledWith('a@b.com', 'Senha@12345', undefined);

      auth.autenticar.and.returnValue(of({ accessToken: 't', expires: '' }));
      c.form.patchValue({ codigo: ' 123 456 ' });
      c.entrar();

      expect(auth.autenticar).toHaveBeenCalledWith('a@b.com', 'Senha@12345', '123 456');
      expect(router.navigateByUrl).toHaveBeenCalled();
    });

    it('sem 2FA o código nem aparece e o pedido segue sem ele', () => {
      configurar();
      const fixture = TestBed.createComponent(LoginComponent);
      fixture.detectChanges();
      fixture.componentInstance.form.patchValue({ email: 'a@b.com', senha: 'Senha@12345' });
      auth.autenticar.and.returnValue(of({ accessToken: 't', expires: '' }));

      fixture.componentInstance.entrar();
      fixture.detectChanges();

      expect(fixture.componentInstance.pedirCodigo()).toBeFalse();
      expect(fixture.nativeElement.textContent).not.toContain('Código de verificação');
      expect(auth.autenticar).toHaveBeenCalledWith('a@b.com', 'Senha@12345', undefined);
    });
  });

  describe('EsqueciSenhaComponent', () => {
    it('e-mail inválido não chama a API', () => {
      configurar();
      const fixture = TestBed.createComponent(EsqueciSenhaComponent);
      fixture.detectChanges();
      fixture.componentInstance.form.patchValue({ email: 'isto-nao-e-email' });

      fixture.componentInstance.enviar();

      expect(auth.solicitarRecuperacaoDeSenha).not.toHaveBeenCalled();
    });

    it('depois de enviar mostra a mensagem neutra (nunca diz se a conta existe)', () => {
      configurar();
      const fixture = TestBed.createComponent(EsqueciSenhaComponent);
      fixture.detectChanges();
      auth.solicitarRecuperacaoDeSenha.and.returnValue(of({}));
      fixture.componentInstance.form.patchValue({ email: 'ninguem@teste.com' });

      fixture.componentInstance.enviar();
      fixture.detectChanges();

      expect(auth.solicitarRecuperacaoDeSenha).toHaveBeenCalledOnceWith('ninguem@teste.com');
      expect(fixture.nativeElement.textContent).toContain('Se existir uma conta com este e-mail');
    });

    it('excesso de pedidos (429) mostra aviso próprio no formulário', () => {
      configurar();
      const fixture = TestBed.createComponent(EsqueciSenhaComponent);
      fixture.detectChanges();
      auth.solicitarRecuperacaoDeSenha.and.returnValue(throwError(() => ({ status: 429 })));
      fixture.componentInstance.form.patchValue({ email: 'a@b.com' });

      fixture.componentInstance.enviar();

      expect(fixture.componentInstance.erro()).toContain('Muitas tentativas');
      expect(fixture.componentInstance.enviado()).toBeFalse();
    });
  });

  describe('RedefinirSenhaComponent', () => {
    it('link sem e-mail ou token mostra "inválido" e oferece pedir outro', () => {
      configurar('');
      const fixture = TestBed.createComponent(RedefinirSenhaComponent);
      fixture.detectChanges();

      expect(fixture.componentInstance.linkValido).toBeFalse();
      expect(fixture.nativeElement.textContent).toContain('Link incompleto ou inválido');
      expect(fixture.nativeElement.querySelector('a[href="/esqueci-senha"]')).not.toBeNull();
    });

    it('lê e-mail e token do fragmento (#) e os usa na redefinição', () => {
      configurar('email=ana%40loja.com&token=AbC_-123');
      const fixture = TestBed.createComponent(RedefinirSenhaComponent);
      fixture.detectChanges();
      auth.redefinirSenha.and.returnValue(of(undefined));
      fixture.componentInstance.form.patchValue({ senha: 'NovaSenha9', confirmar: 'NovaSenha9' });

      fixture.componentInstance.salvar();

      expect(fixture.componentInstance.email).toBe('ana@loja.com');
      expect(auth.redefinirSenha).toHaveBeenCalledOnceWith('ana@loja.com', 'AbC_-123', 'NovaSenha9', 'NovaSenha9');
      expect(router.navigate).toHaveBeenCalledWith(['/login']);
    });

    it('senha fraca ou confirmação diferente não chama a API', () => {
      configurar('email=a%40b.com&token=t');
      const fixture = TestBed.createComponent(RedefinirSenhaComponent);
      fixture.detectChanges();

      fixture.componentInstance.form.patchValue({ senha: 'fraca', confirmar: 'fraca' });
      fixture.componentInstance.salvar();
      fixture.componentInstance.form.patchValue({ senha: 'NovaSenha9', confirmar: 'OutraSenha9' });
      fixture.componentInstance.salvar();

      expect(auth.redefinirSenha).not.toHaveBeenCalled();
      expect(fixture.componentInstance.form.controls.confirmar.hasError('mismatch')).toBeTrue();
    });

    it('mostra a mensagem do servidor quando o link expirou', () => {
      configurar('email=a%40b.com&token=t');
      const fixture = TestBed.createComponent(RedefinirSenhaComponent);
      fixture.detectChanges();
      auth.redefinirSenha.and.returnValue(throwError(() => ({ status: 400, error: ['Requisição inválida', 'Link inválido ou expirado. Peça uma nova redefinição de senha.'] })));
      fixture.componentInstance.form.patchValue({ senha: 'NovaSenha9', confirmar: 'NovaSenha9' });

      fixture.componentInstance.salvar();
      fixture.detectChanges();

      expect(fixture.componentInstance.erro()).toBe('Link inválido ou expirado. Peça uma nova redefinição de senha.');
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });
});
