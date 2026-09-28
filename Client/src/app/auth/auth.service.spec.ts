import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuthService]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('não considera logado quando nunca houve login', () => {
    expect(service.isLoggedIn()).toBeFalse();
    expect(service.getAccessToken()).toBeNull();
  });

  it('guarda o token em memória após autenticar com sucesso', () => {
    service.autenticar('usuario@teste.com', 'Senha@123').subscribe();

    const req = httpMock.expectOne('https://localhost:7064/api/auth/autenticar');
    expect(req.request.method).toBe('POST');
    req.flush({
      chave: 'token-fake',
      expiracao: new Date(Date.now() + 60_000).toISOString(),
      usuarioAutenticado: { id: '1', nomeCompleto: 'Usuário Teste', email: 'usuario@teste.com', cargo: 'Empresa' }
    });

    expect(service.isLoggedIn()).toBeTrue();
    expect(service.getAccessToken()).toBe('token-fake');
    expect(service.getUsuario()?.email).toBe('usuario@teste.com');
  });

  it('considera o token expirado quando a data de expiração já passou', () => {
    service.autenticar('usuario@teste.com', 'Senha@123').subscribe();

    const req = httpMock.expectOne('https://localhost:7064/api/auth/autenticar');
    req.flush({
      chave: 'token-fake',
      expiracao: new Date(Date.now() - 60_000).toISOString(), // já expirado
      usuarioAutenticado: { id: '1', nomeCompleto: 'Usuário Teste', email: 'usuario@teste.com', cargo: 'Empresa' }
    });

    expect(service.isTokenExpirado()).toBeTrue();
    expect(service.isLoggedIn()).toBeFalse();
  });

  it('limparStorage apaga a sessão em memória', () => {
    service.autenticar('usuario@teste.com', 'Senha@123').subscribe();
    httpMock.expectOne('https://localhost:7064/api/auth/autenticar').flush({
      chave: 'token-fake',
      expiracao: new Date(Date.now() + 60_000).toISOString(),
      usuarioAutenticado: { id: '1', nomeCompleto: 'Usuário Teste', email: 'usuario@teste.com', cargo: 'Empresa' }
    });

    service.limparStorage();

    expect(service.isLoggedIn()).toBeFalse();
    expect(service.getUsuario()).toBeNull();
  });

  it('entrarComRosto chama o endpoint de login facial e guarda o token igual ao login por senha', () => {
    service.entrarComRosto('usuario@teste.com', 'imagem-base64').subscribe();

    const req = httpMock.expectOne('https://localhost:7064/api/auth/entrar-facial');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'usuario@teste.com', imagemBase64: 'imagem-base64' });
    req.flush({
      chave: 'token-facial',
      expiracao: new Date(Date.now() + 60_000).toISOString(),
      usuarioAutenticado: { id: '1', nomeCompleto: 'Usuário Teste', email: 'usuario@teste.com', cargo: 'Empresa' }
    });

    expect(service.isLoggedIn()).toBeTrue();
    expect(service.getAccessToken()).toBe('token-facial');
  });
});
