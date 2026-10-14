import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { NotificacaoService } from '../../core/notificacao.service';
import { criarNotificacaoFalsa } from '../../core/testing/notificacao-fake.spec';
import { of } from 'rxjs';
import { ClientesListComponent } from './clientes-list.component';
import { ClientesService } from './clientes.service';
import { violacoesNosDoisTemas } from '../../core/testing/acessibilidade';

describe('ClientesListComponent — menu "Novo Cliente"', () => {
  let fixture: ComponentFixture<ClientesListComponent>;
  let router: Router;

  const botaoNovo = () => fixture.nativeElement.querySelector('button[aria-haspopup="menu"]') as HTMLButtonElement;
  const itensDoMenu = () => Array.from<HTMLElement>(document.querySelectorAll('[data-slot="dropdown-menu-item"]'));

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ClientesListComponent],
      providers: [
        provideRouter([]),
        { provide: ClientesService, useValue: { listar: () => of([]) } },
        { provide: NotificacaoService, useValue: criarNotificacaoFalsa() }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(ClientesListComponent);
    fixture.detectChanges();
  });

  it('o botão "Novo cliente" abre um menu com Pessoa física e Pessoa jurídica (Spartan, sem depender do JS do Bootstrap)', () => {
    expect(itensDoMenu().length).toBe(0);

    botaoNovo().click();
    fixture.detectChanges();

    expect(itensDoMenu().map(i => i.textContent?.trim())).toEqual(['Pessoa física', 'Pessoa jurídica']);
    expect(botaoNovo().getAttribute('aria-expanded')).toBe('true');
  });

  it('escolher um item do menu leva ao formulário certo', () => {
    botaoNovo().click();
    fixture.detectChanges();

    itensDoMenu()[1].click();

    expect(router.navigate).toHaveBeenCalledWith(['/clientes/new/pj']);
  });

  it('navega para as rotas que existem em app.routes (pf / pj)', () => {
    fixture.componentInstance.novoCliente('pf');
    fixture.componentInstance.novoCliente('pj');

    expect(router.navigate).toHaveBeenCalledWith(['/clientes/new/pf']);
    expect(router.navigate).toHaveBeenCalledWith(['/clientes/new/pj']);
  });

  it('novo() usa as mesmas rotas (pf/pj), não os nomes antigos PessoaFisica/PessoaJuridica', () => {
    fixture.componentInstance.modoLista = 'pf';
    fixture.componentInstance.novo();
    fixture.componentInstance.modoLista = 'pj';
    fixture.componentInstance.novo();

    expect(router.navigate).toHaveBeenCalledWith(['/clientes/new/pf']);
    expect(router.navigate).toHaveBeenCalledWith(['/clientes/new/pj']);
  });
  describe('obterDocumentoPrincipal', () => {
    it('lista geral (só documentoPrincipal): formata CPF e CNPJ — a coluna ficava vazia', () => {
      const c = fixture.componentInstance;

      expect(c.obterDocumentoPrincipal({ tipoCliente: 'PessoaFisica', documentoPrincipal: '12345678900' })).toBe('123.456.789-00');
      expect(c.obterDocumentoPrincipal({ tipoCliente: 'PessoaJuridica', documentoPrincipal: '12345678000199' })).toBe('12.345.678/0001-99');
    });

    it('listas pf/pj (cpf/cnpj no objeto) continuam funcionando', () => {
      const c = fixture.componentInstance;

      expect(c.obterDocumentoPrincipal({ cpf: '12345678900' })).toBe('123.456.789-00');
      expect(c.obterDocumentoPrincipal({ cnpj: '12345678000199' })).toBe('12.345.678/0001-99');
    });

    it('já formatado é mantido e ausente vira "—"', () => {
      const c = fixture.componentInstance;

      expect(c.obterDocumentoPrincipal({ documentoPrincipal: '123.456.789-00' })).toBe('123.456.789-00');
      expect(c.obterDocumentoPrincipal({})).toBe('—');
    });
  });

  it('acessibilidade (axe-core, WCAG 2.2 AA): sem violações nos temas claro e escuro', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(await violacoesNosDoisTemas(fixture.nativeElement)).toEqual([]);
  });
});
