import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { ClientesListComponent } from './clientes-list.component';
import { ClientesService } from './clientes.service';

describe('ClientesListComponent — menu "Novo Cliente"', () => {
  let fixture: ComponentFixture<ClientesListComponent>;
  let router: Router;

  const menu = () => fixture.nativeElement.querySelector('.dropdown-menu') as HTMLElement;
  const botao = () => fixture.nativeElement.querySelector('.dropdown-toggle') as HTMLButtonElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ClientesListComponent],
      providers: [
        provideRouter([]),
        { provide: ClientesService, useValue: { listar: () => of([]) } },
        { provide: MatSnackBar, useValue: { open: () => {} } }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(ClientesListComponent);
    fixture.detectChanges();
  });

  it('abre o menu ao clicar no botão (sem depender do JS do Bootstrap)', () => {
    expect(menu().classList).not.toContain('show');

    botao().click();
    fixture.detectChanges();

    expect(menu().classList).toContain('show');
    expect(botao().getAttribute('aria-expanded')).toBe('true');
  });

  it('fecha o menu ao clicar fora ou apertar Esc', () => {
    botao().click();
    fixture.detectChanges();

    document.body.click();
    fixture.detectChanges();
    expect(menu().classList).not.toContain('show');

    botao().click();
    fixture.detectChanges();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(menu().classList).not.toContain('show');
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
});
