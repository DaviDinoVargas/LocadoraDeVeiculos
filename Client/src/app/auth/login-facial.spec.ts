import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { NotificacaoService } from '../core/notificacao.service';
import { criarNotificacaoFalsa } from '../core/testing/notificacao-fake.spec';
import { WebcamCaptureService, WebcamErro } from '../shared/webcam-capture.service';
import { AccessToken } from './auth.models';
import { AuthService } from './auth.service';
import { LoginComponent } from './login.component';

const TOKEN = { chave: 'jwt' } as unknown as AccessToken;

describe('LoginComponent — entrar com reconhecimento facial', () => {
  let auth: jasmine.SpyObj<AuthService>;
  let webcam: jasmine.SpyObj<WebcamCaptureService>;
  let notificacao: jasmine.SpyObj<NotificacaoService>;
  let router: Router;

  const montar = (opcoes: { logado?: boolean; returnUrl?: string } = {}) => {
    auth = jasmine.createSpyObj('AuthService', ['autenticar', 'isLoggedIn', 'entrarComRosto']);
    auth.isLoggedIn.and.returnValue(opcoes.logado ?? false);
    webcam = jasmine.createSpyObj('WebcamCaptureService', ['ligar', 'desligar', 'capturarFrameBase64']);
    webcam.ligar.and.resolveTo();
    webcam.capturarFrameBase64.and.returnValue('data:image/jpeg;base64,AAAA');
    notificacao = criarNotificacaoFalsa();

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: NotificacaoService, useValue: notificacao },
        { provide: WebcamCaptureService, useValue: webcam },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParams: opcoes.returnUrl ? { returnUrl: opcoes.returnUrl } : {} } } }
      ]
    });
    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);
    spyOn(router, 'navigate').and.resolveTo(true);

    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    return fixture;
  };

  const comEmail = (f: ReturnType<typeof montar>, email = 'ana@exemplo.com') => f.componentInstance.form.patchValue({ email });

  describe('ligar o modo facial', () => {
    it('sem e-mail válido não liga a câmera e explica o que falta', async () => {
      const f = montar();

      await f.componentInstance.alternarModoFacial();

      expect(f.componentInstance.erro()).toBe('Informe seu e-mail para entrar com reconhecimento facial.');
      expect(f.componentInstance.modoFacial()).toBeFalse();
      expect(f.componentInstance.form.controls.email.touched).toBeTrue();
      expect(webcam.ligar).not.toHaveBeenCalled();
    });

    it('com e-mail liga a câmera no vídeo da tela e marca a câmera como ativa; clicar de novo desliga tudo', async () => {
      const f = montar();
      comEmail(f);

      await f.componentInstance.alternarModoFacial();

      expect(webcam.ligar).toHaveBeenCalledOnceWith(f.nativeElement.querySelector('video'));
      expect(f.componentInstance.modoFacial()).toBeTrue();
      expect(f.componentInstance.cameraAtiva()).toBeTrue();

      await f.componentInstance.alternarModoFacial();

      expect(webcam.desligar).toHaveBeenCalled();
      expect(f.componentInstance.modoFacial()).toBeFalse();
      expect(f.componentInstance.cameraAtiva()).toBeFalse();
    });

    it('falha da câmera mostra a mensagem específica (permissão negada, câmera em uso...) e volta ao login normal', async () => {
      const f = montar();
      comEmail(f);
      webcam.ligar.and.rejectWith(new WebcamErro('permissao', 'Permita o acesso à câmera nas configurações do navegador.'));

      await f.componentInstance.alternarModoFacial();

      expect(f.componentInstance.erro()).toBe('Permita o acesso à câmera nas configurações do navegador.');
      expect(f.componentInstance.modoFacial()).toBeFalse();
      expect(f.componentInstance.cameraAtiva()).toBeFalse();
    });

    it('erro desconhecido ao ligar a câmera cai numa mensagem genérica, sem vazar o erro técnico', async () => {
      const f = montar();
      comEmail(f);
      webcam.ligar.and.rejectWith(new Error('NotReadableError: Could not start video source 0x80070020'));

      await f.componentInstance.alternarModoFacial();

      expect(f.componentInstance.erro()).toBe('Não foi possível acessar a câmera. Verifique as permissões do navegador.');
      expect(f.componentInstance.erro()).not.toContain('0x8007');
    });
  });

  describe('entrar com o rosto', () => {
    const preparar = async () => {
      const f = montar({ returnUrl: '/alugueis' });
      comEmail(f);
      await f.componentInstance.alternarModoFacial();
      return f;
    };

    it('sucesso: manda e-mail e imagem capturada, avisa, desliga a câmera e vai para a tela pedida', async () => {
      const f = await preparar();
      auth.entrarComRosto.and.returnValue(of(TOKEN));

      f.componentInstance.entrarComRosto();

      expect(auth.entrarComRosto).toHaveBeenCalledOnceWith('ana@exemplo.com', 'data:image/jpeg;base64,AAAA', undefined);
      expect(notificacao.sucesso).toHaveBeenCalledWith('Login facial realizado com sucesso');
      expect(webcam.desligar).toHaveBeenCalled();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/alugueis');
      expect(f.componentInstance.carregando()).toBeFalse();
    });

    it('com o código do autenticador preenchido, manda o código sem espaços junto com o rosto', async () => {
      const f = await preparar();
      auth.entrarComRosto.and.returnValue(of(TOKEN));
      f.componentInstance.form.patchValue({ codigo: '  123456 ' });

      f.componentInstance.entrarComRosto();

      expect(auth.entrarComRosto).toHaveBeenCalledOnceWith('ana@exemplo.com', 'data:image/jpeg;base64,AAAA', '123456');
    });

    it('conta com verificação em duas etapas (428): pede o código e orienta a capturar de novo', async () => {
      const f = await preparar();
      auth.entrarComRosto.and.returnValue(throwError(() => ({ status: 428 })));

      f.componentInstance.entrarComRosto();

      expect(f.componentInstance.pedirCodigo()).toBeTrue();
      expect(f.componentInstance.erro()).toContain('verificação em duas etapas');
      expect(f.componentInstance.erro()).toContain('capture de novo');
      expect(router.navigateByUrl).not.toHaveBeenCalled();
    });

    it('rosto não detectado (422) e qualquer outra falha têm mensagens diferentes, e nenhuma revela se o e-mail existe', async () => {
      const f = await preparar();

      auth.entrarComRosto.and.returnValue(throwError(() => ({ status: 422 })));
      f.componentInstance.entrarComRosto();
      expect(f.componentInstance.erro()).toBe('Nenhum rosto detectado. Aproxime-se da câmera e tente novamente.');

      auth.entrarComRosto.and.returnValue(throwError(() => ({ status: 401 })));
      f.componentInstance.entrarComRosto();
      expect(f.componentInstance.erro()).toBe('Não foi possível confirmar sua identidade. Tente novamente ou entre com sua senha.');
      expect(f.componentInstance.carregando()).toBeFalse();
      expect(f.componentInstance.pedirCodigo()).toBeFalse();
    });

    it('sem imagem capturada (câmera ainda sem quadro) não chama a API', async () => {
      const f = await preparar();
      webcam.capturarFrameBase64.and.returnValue(null);

      f.componentInstance.entrarComRosto();

      expect(auth.entrarComRosto).not.toHaveBeenCalled();
      expect(f.componentInstance.carregando()).toBeFalse();
    });

    it('enquanto uma tentativa está em andamento, um segundo clique é ignorado (não manda o mesmo rosto duas vezes)', async () => {
      const f = await preparar();
      const resposta = new Subject<never>();
      auth.entrarComRosto.and.returnValue(resposta);

      f.componentInstance.entrarComRosto();
      f.componentInstance.entrarComRosto();

      expect(auth.entrarComRosto).toHaveBeenCalledTimes(1);
      expect(f.componentInstance.carregando()).toBeTrue();
    });

    it('sem e-mail no formulário não tenta entrar', async () => {
      const f = await preparar();
      f.componentInstance.form.patchValue({ email: '' });

      f.componentInstance.entrarComRosto();

      expect(auth.entrarComRosto).not.toHaveBeenCalled();
    });
  });

  describe('ciclo de vida', () => {
    it('quem já está logado é levado direto para a tela pedida, sem ver o formulário de entrada', () => {
      montar({ logado: true, returnUrl: '/devolucoes' });

      expect(router.navigateByUrl).toHaveBeenCalledWith('/devolucoes');
    });

    it('sair da tela desliga a câmera (a webcam não pode ficar presa)', () => {
      const f = montar();

      f.destroy();

      expect(webcam.desligar).toHaveBeenCalled();
    });

    it('se a tela pedida não existir, cai na home em vez de ficar parado', async () => {
      const f = montar({ returnUrl: '/rota-que-nao-existe' });
      (router.navigateByUrl as jasmine.Spy).and.resolveTo(false);
      f.componentInstance.form.patchValue({ email: 'a@b.com', senha: 'Senha@12345' });
      auth.autenticar.and.returnValue(of(TOKEN));

      f.componentInstance.entrar();
      await f.whenStable();

      expect(router.navigate).toHaveBeenCalledWith(['/home']);
    });
  });
});
