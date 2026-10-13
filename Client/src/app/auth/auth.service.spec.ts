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
  describe('cabeçalho anti-CSRF', () => {
    it('rotacionar e sair enviam X-Locadora-Client (a API recusa as chamadas sem ele)', () => {
      service.rotacionarToken().subscribe({ error: () => undefined });
      const rotacao = httpMock.expectOne('/api/auth/rotacionar');
      expect(rotacao.request.headers.get('X-Locadora-Client')).toBe('web');
      expect(rotacao.request.withCredentials).toBeTrue();
      rotacao.flush([], { status: 401, statusText: 'Unauthorized' });

      service.sair().subscribe({ error: () => undefined });
      const saida = httpMock.expectOne('/api/auth/sair');
      expect(saida.request.headers.get('X-Locadora-Client')).toBe('web');
      saida.flush(null);
    });

    it('com sessão ativa, mantém o Authorization junto do cabeçalho', () => {
      service.autenticar('a@b.com', 'Senha@123').subscribe();
      httpMock.expectOne('/api/auth/autenticar').flush({
        chave: 'token-fake',
        expiracao: new Date(Date.now() + 60_000).toISOString(),
        usuarioAutenticado: { id: '1', nomeCompleto: 'A', email: 'a@b.com', cargo: 'Empresa' }
      });

      service.rotacionarToken().subscribe({ error: () => undefined });
      const req = httpMock.expectOne('/api/auth/rotacionar');
      expect(req.request.headers.get('Authorization')).toBe('Bearer token-fake');
      expect(req.request.headers.get('X-Locadora-Client')).toBe('web');
      req.flush([], { status: 401, statusText: 'Unauthorized' });
    });
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

  describe('rosto', () => {
    it('consulta o status pela Web API', () => {
      let cadastrado: boolean | undefined;
      service.statusRosto().subscribe(r => (cadastrado = r.cadastrado));

      const req = httpMock.expectOne('/api/auth/rosto');
      expect(req.request.method).toBe('GET');
      req.flush({ cadastrado: true });

      expect(cadastrado).toBeTrue();
    });

    it('verifica a identidade enviando só a imagem (o servidor descobre quem é pelo JWT)', () => {
      service.verificarRosto('data:image/jpeg;base64,abc').subscribe();

      const req = httpMock.expectOne('/api/auth/rosto/verificar');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ imagemBase64: 'data:image/jpeg;base64,abc' });
      req.flush({ confirmado: true, confianca: 0.97 });
    });

    it('apaga o rosto do usuário logado com DELETE /auth/rosto', () => {
      service.removerRosto().subscribe();

      const req = httpMock.expectOne('/api/auth/rosto');
      expect(req.request.method).toBe('DELETE');
      req.flush(null, { status: 204, statusText: 'No Content' });
    });
  });

  describe('consentimento, recuperação de senha e segundo fator', () => {
    it('busca o termo de consentimento no servidor', () => {
      service.obterTermoDeConsentimento().subscribe();

      const req = httpMock.expectOne('/api/auth/rosto/consentimento');
      expect(req.request.method).toBe('GET');
      req.flush({ versao: 'v1', texto: 't' });
    });

    it('cadastrar o rosto envia o aceite e a versão do texto exibido', () => {
      service.cadastrarRosto('data:image/jpeg;base64,abc', '2026-10-v1').subscribe();

      const req = httpMock.expectOne('/api/auth/rosto');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ imagemBase64: 'data:image/jpeg;base64,abc', consentimento: true, versaoDoTexto: '2026-10-v1' });
      req.flush({ amostras: 1 });
    });

    it('login envia o código do autenticador só quando ele existe', () => {
      service.autenticar('a@b.com', 'Senha@12345').subscribe({ error: () => {} });
      const sem = httpMock.expectOne('/api/auth/autenticar');
      expect(sem.request.body).toEqual({ email: 'a@b.com', senha: 'Senha@12345' });
      sem.flush(null, { status: 428, statusText: 'Precondition Required' });

      service.autenticar('a@b.com', 'Senha@12345', '123456').subscribe({ error: () => {} });
      const com = httpMock.expectOne('/api/auth/autenticar');
      expect(com.request.body).toEqual({ email: 'a@b.com', senha: 'Senha@12345', codigoAutenticador: '123456' });
      com.flush(null, { status: 401, statusText: 'x' });
    });

    it('esqueci-senha e redefinir-senha usam os endpoints públicos', () => {
      service.solicitarRecuperacaoDeSenha('a@b.com').subscribe();
      const a = httpMock.expectOne('/api/auth/esqueci-senha');
      expect(a.request.body).toEqual({ email: 'a@b.com' });
      a.flush({});

      service.redefinirSenha('a@b.com', 'tok', 'Nova@12345', 'Nova@12345').subscribe();
      const b = httpMock.expectOne('/api/auth/redefinir-senha');
      expect(b.request.body).toEqual({ email: 'a@b.com', token: 'tok', novaSenha: 'Nova@12345', confirmarNovaSenha: 'Nova@12345' });
      b.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('trocar a senha e encerrar a conta limpam a sessão local (o servidor derrubou as sessões)', () => {
      const limpar = spyOn(service, 'limparStorage').and.callThrough();
      service.alterarSenha('Velha@12345', 'Nova@12345', 'Nova@12345').subscribe();
      httpMock.expectOne('/api/auth/alterar-senha').flush(null, { status: 204, statusText: 'No Content' });
      expect(limpar).toHaveBeenCalledTimes(1);

      service.encerrarConta('Senha@12345').subscribe();
      const req = httpMock.expectOne('/api/auth/encerrar-conta');
      expect(req.request.body).toEqual({ senha: 'Senha@12345' });
      req.flush(null, { status: 204, statusText: 'No Content' });
      expect(limpar).toHaveBeenCalledTimes(2);
    });

    it('endpoints de 2FA: status, iniciar, ativar e desativar', () => {
      service.statusDoisFatores().subscribe();
      expect(httpMock.expectOne('/api/auth/2fa').request.method).toBe('GET');
      httpMock.match('/api/auth/2fa').forEach(r => r.flush({ habilitado: false, codigosDeRecuperacaoRestantes: 0 }));

      service.iniciarDoisFatores().subscribe();
      httpMock.expectOne('/api/auth/2fa/iniciar').flush({ chave: 'k', uri: 'u' });

      service.ativarDoisFatores('123456').subscribe();
      const at = httpMock.expectOne('/api/auth/2fa/ativar');
      expect(at.request.body).toEqual({ codigo: '123456' });
      at.flush({ codigos: [] });

      service.desativarDoisFatores('Senha@12345', '123456').subscribe();
      const de = httpMock.expectOne('/api/auth/2fa/desativar');
      expect(de.request.body).toEqual({ senha: 'Senha@12345', codigo: '123456' });
      de.flush(null, { status: 204, statusText: 'No Content' });
    });
  });
});
