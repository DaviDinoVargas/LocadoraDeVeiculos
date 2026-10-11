import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { NotificacaoService } from './notificacao.service';
import { criarNotificacaoFalsa } from './testing/notificacao-fake.spec';
import { Observable, Subject, of } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { roleGuard } from './role.guard';
import { EMPRESA_E_FUNCIONARIO, SOMENTE_EMPRESA, cargoPodeAcessar } from './permissoes';

describe('cargoPodeAcessar', () => {
  it('libera quando a rota não define papéis', () => {
    expect(cargoPodeAcessar('Funcionario', undefined)).toBeTrue();
  });

  it('exige que o cargo esteja na lista', () => {
    expect(cargoPodeAcessar('Empresa', SOMENTE_EMPRESA)).toBeTrue();
    expect(cargoPodeAcessar('Funcionario', SOMENTE_EMPRESA)).toBeFalse();
    expect(cargoPodeAcessar('Funcionario', EMPRESA_E_FUNCIONARIO)).toBeTrue();
  });

  it('nega quando não há cargo (usuário sem sessão/cargo)', () => {
    expect(cargoPodeAcessar(null, SOMENTE_EMPRESA)).toBeFalse();
    expect(cargoPodeAcessar(undefined, EMPRESA_E_FUNCIONARIO)).toBeFalse();
  });
});

describe('roleGuard', () => {
  let cargo: string | undefined;
  let notificacao: jasmine.SpyObj<NotificacaoService>;

  const rota = (roles?: readonly string[]) =>
    ({ data: roles ? { roles } : {} }) as unknown as ActivatedRouteSnapshot;
  const executar = (roles?: readonly string[]) =>
    new Promise<boolean | UrlTree>(resolve =>
      (TestBed.runInInjectionContext(() => roleGuard(rota(roles), {} as RouterStateSnapshot)) as Observable<boolean | UrlTree>)
        .subscribe(resolve));

  beforeEach(() => {
    notificacao = criarNotificacaoFalsa();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: NotificacaoService, useValue: notificacao },
        { provide: AuthService, useValue: { getUsuario: () => (cargo ? { cargo } : null), garantirSessao: () => of(true) } }
      ]
    });
  });

  it('deixa a Empresa entrar em rota só-Empresa', async () => {
    cargo = 'Empresa';
    expect(await executar(SOMENTE_EMPRESA)).toBeTrue();
    expect(notificacao.aviso).not.toHaveBeenCalled();
  });

  it('manda o Funcionario para /home com aviso quando a rota é só-Empresa', async () => {
    cargo = 'Funcionario';
    const resultado = await executar(SOMENTE_EMPRESA);

    expect(resultado instanceof UrlTree).toBeTrue();
    expect(TestBed.inject(Router).serializeUrl(resultado as UrlTree)).toBe('/home');
    expect(notificacao.aviso).toHaveBeenCalled();
  });

  it('deixa o Funcionario entrar em rota liberada para os dois papéis', async () => {
    cargo = 'Funcionario';
    expect(await executar(EMPRESA_E_FUNCIONARIO)).toBeTrue();
  });

  it('REGRESSÃO (F5): espera a sessão ser restaurada antes de olhar o cargo, em vez de negar por usuário ainda vazio', async () => {
    const restauracao = new Subject<boolean>();
    TestBed.inject(AuthService).garantirSessao = () => restauracao;
    cargo = undefined; // logo após o F5 o usuário ainda não existe

    let resultado: boolean | UrlTree | undefined;
    const pendente = executar(EMPRESA_E_FUNCIONARIO).then(r => (resultado = r));
    expect(resultado).toBeUndefined(); // não decidiu ainda

    cargo = 'Funcionario'; // a restauração preenche o usuário...
    restauracao.next(true); // ...e só então o guard decide
    await pendente;

    expect(resultado).toBeTrue();
    expect(notificacao.aviso).not.toHaveBeenCalled();
  });

  it('sem sessão deixa o AuthGuard decidir (não abre aviso de permissão)', async () => {
    TestBed.inject(AuthService).garantirSessao = () => of(false);

    expect(await executar(SOMENTE_EMPRESA)).toBeTrue();
    expect(notificacao.aviso).not.toHaveBeenCalled();
  });
});
