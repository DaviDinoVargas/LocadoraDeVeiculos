import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorInterceptor } from './http.interceptor';
import { SILENCIAR_ERRO } from '../core/http-context';

describe('HttpErrorInterceptor — mensagens de erro', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let snack: jasmine.SpyObj<MatSnackBar>;

  beforeEach(() => {
    snack = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([HttpErrorInterceptor])),
        provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: snack }
      ]
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    spyOn(console, 'error'); // o interceptor registra o erro no console
  });

  afterEach(() => backend.verify());

  const falhar = (status: number, corpo: object | string | null, opcoes: { context?: HttpContext } = {}) => {
    http.get('/api/x', opcoes).subscribe({ error: () => {} });
    backend.expectOne('/api/x').flush(corpo, { status, statusText: 'erro' });
  };

  it('409 mostra a mensagem de negócio que o servidor mandou (antes: "Erro na requisição")', () => {
    falhar(409, ['O automóvel já está reservado para este período.']);

    expect(snack.open.calls.mostRecent().args[0]).toBe('O automóvel já está reservado para este período.');
  });

  it('422 (exclusão bloqueada) mostra o motivo do servidor', () => {
    falhar(422, ['Não é possível excluir: existem aluguéis vinculados.']);

    expect(snack.open.calls.mostRecent().args[0]).toBe('Não é possível excluir: existem aluguéis vinculados.');
  });

  it('400 com array de mensagens junta todas (antes: "Dados inválidos")', () => {
    falhar(400, ['Campo A inválido.', 'Campo B inválido.']);

    expect(snack.open.calls.mostRecent().args[0]).toBe('Campo A inválido. Campo B inválido.');
  });

  it('400 no formato de validação do ASP.NET continua funcionando', () => {
    falhar(400, { errors: { Nome: ['Nome obrigatório'], Cpf: ['Cpf inválido'] } });

    expect(snack.open.calls.mostRecent().args[0]).toBe('Nome obrigatório, Cpf inválido');
  });

  it('404 sem corpo usa o texto padrão', () => {
    falhar(404, null);

    expect(snack.open.calls.mostRecent().args[0]).toBe('Recurso não encontrado.');
  });

  it('500 nunca repassa texto interno: mensagem genérica', () => {
    falhar(500, ['detalhe interno do servidor']);

    expect(snack.open.calls.mostRecent().args[0]).toBe('Erro interno do servidor.');
  });

  it('403 e 401 têm mensagens próprias', () => {
    falhar(403, null);
    expect(snack.open.calls.mostRecent().args[0]).toBe('Acesso negado.');

    falhar(401, null);
    expect(snack.open.calls.mostRecent().args[0]).toBe('Não autorizado. Faça login novamente.');
  });

  it('SILENCIAR_ERRO: quem trata o erro na própria tela não recebe snackbar, mas o erro continua chegando', () => {
    let recebeu = false;
    http.get('/api/x', { context: new HttpContext().set(SILENCIAR_ERRO, true) }).subscribe({ error: () => (recebeu = true) });
    backend.expectOne('/api/x').flush(['qualquer'], { status: 400, statusText: 'erro' });

    expect(snack.open).not.toHaveBeenCalled();
    expect(recebeu).toBeTrue();
  });
});
