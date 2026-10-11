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

    const req = httpMock.expectOne('/api/auth/autenticar');
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

    const req = httpMock.expectOne('/api/auth/autenticar');
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
    httpMock.expectOne('/api/auth/autenticar').flush({
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

    const req = httpMock.expectOne('/api/auth/entrar-facial');
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
  describe('garantirSessao', () => {
    const respostaRotacao = () => ({
      chave: 'token-restaurado',
      expiracao: new Date(Date.now() + 60_000).toISOString(),
      usuarioAutenticado: { id: '1', nomeCompleto: 'Usuário Teste', email: 'u@teste.com', cargo: 'Funcionario' }
    });

    it('já logado: não chama a API', () => {
      service.autenticar('u@teste.com', 'Senha@123').subscribe();
      httpMock.expectOne('/api/auth/autenticar').flush(respostaRotacao());

      let resultado: boolean | undefined;
      service.garantirSessao().subscribe(r => (resultado = r));

      expect(resultado).toBeTrue();
      httpMock.expectNone('/api/auth/rotacionar');
    });

    it('chamadas simultâneas (AuthGuard + roleGuard após F5) usam UMA só requisição de rotação', () => {
      const resultados: boolean[] = [];
      service.garantirSessao().subscribe(r => resultados.push(r));
      service.garantirSessao().subscribe(r => resultados.push(r));

      // Dois guards => se fossem duas requisições, o refresh token rotativo invalidaria a segunda
      const req = httpMock.expectOne('/api/auth/rotacionar');
      req.flush(respostaRotacao());

      expect(resultados).toEqual([true, true]);
      expect(service.getUsuario()?.cargo).toBe('Funcionario');
    });

    it('sem cookie de sessão (401) devolve false e limpa o estado', () => {
      let resultado: boolean | undefined;
      service.garantirSessao().subscribe(r => (resultado = r));

      httpMock.expectOne('/api/auth/rotacionar').flush(['sem sessão'], { status: 401, statusText: 'Unauthorized' });

      expect(resultado).toBeFalse();
      expect(service.isLoggedIn()).toBeFalse();
    });

    it('depois de terminar, uma nova chamada volta a tentar a restauração', () => {
      service.garantirSessao().subscribe();
      httpMock.expectOne('/api/auth/rotacionar').flush(['x'], { status: 401, statusText: 'Unauthorized' });

      service.garantirSessao().subscribe();
      httpMock.expectOne('/api/auth/rotacionar').flush(respostaRotacao());

      expect(service.isLoggedIn()).toBeTrue();
    });
  });
});
