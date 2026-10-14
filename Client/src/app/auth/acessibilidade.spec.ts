import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NotificacaoService } from '../core/notificacao.service';
import { criarNotificacaoFalsa } from '../core/testing/notificacao-fake.spec';
import { violacoesNosDoisTemas } from '../core/testing/acessibilidade';
import { WebcamCaptureService } from '../shared/webcam-capture.service';
import { AuthService } from './auth.service';
import { LoginComponent } from './login.component';

describe('Acessibilidade (axe-core, WCAG 2.2 AA) — telas de autenticação', () => {
  const configurar = () => {
    const auth = jasmine.createSpyObj('AuthService', ['autenticar', 'registrar', 'isLoggedIn', 'entrarComRosto']);
    auth.isLoggedIn.and.returnValue(false);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: NotificacaoService, useValue: criarNotificacaoFalsa() },
        { provide: WebcamCaptureService, useValue: jasmine.createSpyObj('WebcamCaptureService', ['ligar', 'desligar', 'capturarFrameBase64']) }
      ]
    });
  };

  it('login: sem violações nos temas claro e escuro', async () => {
    configurar();
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();

    expect(await violacoesNosDoisTemas(fixture.nativeElement)).toEqual([]);
  });
});
