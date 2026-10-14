import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { NotificacaoService } from '../core/notificacao.service';
import { violacoesNosDoisTemas } from '../core/testing/acessibilidade';
import { criarNotificacaoFalsa } from '../core/testing/notificacao-fake.spec';
import { AppShellComponent } from '../shared/layout/app-shell.component';
import { WebcamCaptureService } from '../shared/webcam-capture.service';
import { AuthService } from './auth.service';
import { EsqueciSenhaComponent } from './esqueci-senha.component';
import { LoginComponent } from './login.component';
import { RedefinirSenhaComponent } from './redefinir-senha.component';
import { RegisterComponent } from './register.component';

@Component({ imports: [AppShellComponent], template: '<app-shell [comChrome]="true"><p>Conteúdo da página</p></app-shell>' })
class PaginaComCascaComponent {}

describe('Acessibilidade (axe-core, WCAG 2.2 AA) — autenticação e casca do app', () => {
  const configurar = (fragmento: string | null = null) => {
    const auth = jasmine.createSpyObj('AuthService', ['autenticar', 'registrar', 'isLoggedIn', 'entrarComRosto', 'getUsuario', 'solicitarRecuperacaoDeSenha', 'redefinirSenha', 'sair']);
    auth.isLoggedIn.and.returnValue(false);
    auth.getUsuario.and.returnValue({ id: 'u1', nomeCompleto: 'Ana Souza', email: 'ana@exemplo.com', cargo: 'Empresa' });
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth },
        { provide: NotificacaoService, useValue: criarNotificacaoFalsa() },
        { provide: ActivatedRoute, useValue: { snapshot: { fragment: fragmento, queryParams: {} } } },
        { provide: WebcamCaptureService, useValue: jasmine.createSpyObj('WebcamCaptureService', ['ligar', 'desligar', 'capturarFrameBase64']) }
      ]
    });
  };

  const auditar = async (tipo: new () => unknown) => {
    const fixture = TestBed.createComponent(tipo);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return violacoesNosDoisTemas(fixture.nativeElement);
  };

  it('login: sem violações nos temas claro e escuro', async () => {
    configurar();

    expect(await auditar(LoginComponent)).toEqual([]);
  });

  it('cadastro: sem violações nos temas claro e escuro', async () => {
    configurar();

    expect(await auditar(RegisterComponent)).toEqual([]);
  });

  it('esqueci minha senha: sem violações nos temas claro e escuro', async () => {
    configurar();

    expect(await auditar(EsqueciSenhaComponent)).toEqual([]);
  });

  it('redefinir senha (com link válido): sem violações nos temas claro e escuro', async () => {
    configurar('uid=abc&token=xyz');

    expect(await auditar(RedefinirSenhaComponent)).toEqual([]);
  });

  it('casca do app (barra lateral, cabeçalho e menu do usuário): sem violações nos temas claro e escuro', async () => {
    configurar();

    expect(await auditar(PaginaComCascaComponent)).toEqual([]);
  });
});
