import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { AuthInterceptor } from './auth.interceptor';
import { AccessToken, AuthService } from './auth.service';

describe('AuthInterceptor — token, renovação de sessão e 401', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

  const token = (accessToken: string): AccessToken => ({ accessToken, expires: new Date(Date.now() + 900_000).toISOString() });

  const montar = (opcoes: { token: string | null; expirado?: boolean }) => {
    auth = jasmine.createSpyObj('AuthService', ['getAccessToken', 'isTokenExpirado', 'rotacionarToken', 'limparStorage']);
    auth.getAccessToken.and.returnValue(opcoes.token);
    auth.isTokenExpirado.and.returnValue(!!opcoes.expirado);
    router = jasmine.createSpyObj('Router', ['navigate']);
    router.navigate.and.resolveTo(true);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([AuthInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router }
      ]
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  };

  afterEach(() => backend.verify());

  it('manda o token no cabeçalho Authorization', () => {
    montar({ token: 'abc' });

    http.get('/api/x').subscribe();

    const req = backend.expectOne('/api/x');
    expect(req.request.headers.get('Authorization')).toBe('Bearer abc');
    req.flush({});
  });

  it('sem token, ou com token vencido pelo relógio, não manda cabeçalho', () => {
    montar({ token: 'abc', expirado: true });

    http.get('/api/x').subscribe({ error: () => undefined });

    const req = backend.expectOne('/api/x');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({}, { status: 200, statusText: 'ok' });
  });

  it('401 de quem tem sessão renova UMA vez e repete a requisição com o token novo', () => {
    montar({ token: 'velho' });
    const renovacao = new Subject<AccessToken>();
    auth.rotacionarToken.and.returnValue(renovacao);
    let resposta: unknown;

    http.get('/api/x').subscribe(r => (resposta = r));
    backend.expectOne('/api/x').flush({}, { status: 401, statusText: 'Unauthorized' });
    renovacao.next(token('novo'));
    renovacao.complete();

    const repetida = backend.expectOne('/api/x');
    expect(repetida.request.headers.get('Authorization')).toBe('Bearer novo');
    repetida.flush({ ok: true });
    expect(resposta).toEqual({ ok: true });
    expect(auth.rotacionarToken).toHaveBeenCalledTimes(1);
    expect(auth.limparStorage).not.toHaveBeenCalled();
  });

  it('vários 401 ao mesmo tempo compartilham UMA renovação (o refresh token é rotativo)', () => {
    montar({ token: 'velho' });
    const renovacao = new Subject<AccessToken>();
    auth.rotacionarToken.and.returnValue(renovacao);

    http.get('/api/a').subscribe();
    http.get('/api/b').subscribe();
    backend.expectOne('/api/a').flush({}, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/b').flush({}, { status: 401, statusText: 'Unauthorized' });
    renovacao.next(token('novo'));
    renovacao.complete();

    expect(auth.rotacionarToken).toHaveBeenCalledTimes(1);
    backend.expectOne('/api/a').flush({});
    backend.expectOne('/api/b').flush({});
  });

  it('se a renovação falha, encerra a sessão, vai para o login e devolve o erro', () => {
    montar({ token: 'velho' });
    const renovacao = new Subject<AccessToken>();
    auth.rotacionarToken.and.returnValue(renovacao);
    let erro: { status?: number } | undefined;

    http.get('/api/x').subscribe({ error: e => (erro = e) });
    backend.expectOne('/api/x').flush({}, { status: 401, statusText: 'Unauthorized' });
    renovacao.error({ status: 401 });

    expect(auth.limparStorage).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
    expect(erro?.status).toBe(401);
  });

  it('erro da requisição REPETIDA (ex.: 400 de validação) vai para a tela e NÃO derruba a sessão', () => {
    montar({ token: 'velho' });
    const renovacao = new Subject<AccessToken>();
    auth.rotacionarToken.and.returnValue(renovacao);
    let erro: { status?: number } | undefined;

    http.post('/api/x', {}).subscribe({ error: e => (erro = e) });
    backend.expectOne('/api/x').flush({}, { status: 401, statusText: 'Unauthorized' });
    renovacao.next(token('novo'));
    renovacao.complete();
    backend.expectOne('/api/x').flush(['Campo inválido'], { status: 400, statusText: 'Bad Request' });

    expect(erro?.status).toBe(400);
    expect(auth.limparStorage).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('401 de quem NÃO tem sessão não tenta renovar', () => {
    montar({ token: null });
    let erro: { status?: number } | undefined;

    http.get('/api/x').subscribe({ error: e => (erro = e) });
    backend.expectOne('/api/x').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(auth.rotacionarToken).not.toHaveBeenCalled();
    expect(erro?.status).toBe(401);
  });

  it('401 numa rota /auth/ (login errado, refresh recusado) não entra em loop de renovação', () => {
    montar({ token: 'velho' });
    let erro: { status?: number } | undefined;

    http.post('/api/auth/autenticar', {}).subscribe({ error: e => (erro = e) });
    backend.expectOne('/api/auth/autenticar').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(auth.rotacionarToken).not.toHaveBeenCalled();
    expect(erro?.status).toBe(401);
  });

  it('outros erros (500, 403) passam direto, sem renovar', () => {
    montar({ token: 'velho' });

    http.get('/api/x').subscribe({ error: () => undefined });
    backend.expectOne('/api/x').flush({}, { status: 403, statusText: 'Forbidden' });

    expect(auth.rotacionarToken).not.toHaveBeenCalled();
  });
});
