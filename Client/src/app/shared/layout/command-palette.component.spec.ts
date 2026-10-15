import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { BrnDialogRef } from '@spartan-ng/brain/dialog';
import { AuthService } from '../../auth/auth.service';
import { CommandPaletteComponent } from './command-palette.component';

describe('CommandPaletteComponent — Ctrl+K: ir para uma tela pelo teclado', () => {
  let fechar: jasmine.Spy;
  let navegar: jasmine.Spy;

  const montar = (cargo: string) => {
    fechar = jasmine.createSpy('close');
    navegar = jasmine.createSpy('navigate').and.resolveTo(true);
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { getUsuario: () => ({ id: 'u', nomeCompleto: 'Ana', email: 'a@b.com', cargo }) } },
        { provide: BrnDialogRef, useValue: { close: fechar } },
        { provide: Router, useValue: { navigate: navegar } }
      ]
    });
    const fixture = TestBed.createComponent(CommandPaletteComponent);
    fixture.detectChanges();
    return fixture;
  };

  const raiz = (f: { nativeElement: unknown }) => f.nativeElement as HTMLElement;
  const rotulos = (f: { nativeElement: unknown }) =>
    Array.from(raiz(f).querySelectorAll('li[role="option"] span.flex-1')).map(e => e.textContent?.trim());
  const campo = (f: { nativeElement: unknown }) => raiz(f).querySelector('input') as HTMLInputElement;
  const digitar = (f: ReturnType<typeof montar>, texto: string) => {
    campo(f).value = texto;
    campo(f).dispatchEvent(new Event('input'));
    f.detectChanges();
  };
  const tecla = (f: ReturnType<typeof montar>, key: string) => {
    campo(f).dispatchEvent(new KeyboardEvent('keydown', { key, cancelable: true }));
    f.detectChanges();
  };
  const selecionado = (f: ReturnType<typeof montar>) =>
    raiz(f).querySelector('li[aria-selected="true"] span.flex-1')?.textContent?.trim();

  it('a Empresa vê todas as telas, inclusive as de gestão', () => {
    const lista = rotulos(montar('Empresa'));

    for (const tela of ['Home', 'Funcionários', 'Todos os clientes', 'Novo aluguel', 'Devoluções', 'Parceiros', 'Todos os cupons', 'Auditoria', 'Preço do combustível']) {
      expect(lista).withContext(tela).toContain(tela);
    }
  });

  it('o Funcionário não vê as telas que só a Empresa acessa (a paleta não oferece o que a API recusaria)', () => {
    const lista = rotulos(montar('Funcionario'));

    expect(lista).toContain('Novo aluguel');
    expect(lista).toContain('Devoluções');
    for (const tela of ['Funcionários', 'Parceiros', 'Todos os cupons', 'Desafios de cupom', 'Auditoria', 'Preço do combustível']) {
      expect(lista).withContext(tela).not.toContain(tela);
    }
  });

  it('digitar filtra por parte do nome, sem diferenciar maiúsculas, e também pelo nome do grupo', () => {
    const f = montar('Empresa');

    digitar(f, 'DEVOL');
    expect(rotulos(f)).toEqual(['Devoluções']);

    digitar(f, 'cupons');           // "Cupons" é o item pai: os filhos aparecem com ele como grupo
    expect(rotulos(f)).toEqual(['Todos os cupons', 'Desafios de cupom']);
  });

  it('busca sem resultado mostra "Nenhuma tela encontrada." e Enter não navega para lugar nenhum', () => {
    const f = montar('Empresa');

    digitar(f, 'xyz-inexistente');
    tecla(f, 'Enter');

    expect(rotulos(f)).toEqual([]);
    expect(raiz(f).textContent).toContain('Nenhuma tela encontrada.');
    expect(navegar).not.toHaveBeenCalled();
    expect(fechar).not.toHaveBeenCalled();
  });

  it('as setas movem a seleção e dão a volta nas pontas; digitar volta a seleção para o primeiro', () => {
    const f = montar('Empresa');
    const primeiro = selecionado(f);
    const total = rotulos(f).length;

    tecla(f, 'ArrowUp');
    expect(selecionado(f)).toBe(rotulos(f)[total - 1]);   // do primeiro, "para cima" vai ao último
    tecla(f, 'ArrowDown');
    expect(selecionado(f)).toBe(primeiro);                // e "para baixo" do último volta ao primeiro
    tecla(f, 'ArrowDown');
    expect(selecionado(f)).toBe(rotulos(f)[1]);

    digitar(f, 'a');
    expect(selecionado(f)).toBe(rotulos(f)[0]);
  });

  it('Enter abre a tela selecionada e fecha a paleta', () => {
    const f = montar('Empresa');
    digitar(f, 'devol');

    tecla(f, 'Enter');

    expect(navegar).toHaveBeenCalledOnceWith(['/devolucoes'], { queryParams: undefined });
    expect(fechar).toHaveBeenCalledTimes(1);
  });

  it('telas com filtro na URL abrem com o filtro (Em aberto = ?status=em-aberto)', () => {
    const f = montar('Empresa');
    digitar(f, 'em aberto');

    tecla(f, 'Enter');

    expect(navegar).toHaveBeenCalledOnceWith(['/alugueis'], { queryParams: { status: 'em-aberto' } });
  });

  it('clicar numa opção também navega e fecha', () => {
    const f = montar('Empresa');
    digitar(f, 'novo aluguel');

    (raiz(f).querySelector('li[role="option"] button') as HTMLButtonElement).click();

    expect(navegar).toHaveBeenCalledOnceWith(['/alugueis/new'], { queryParams: undefined });
    expect(fechar).toHaveBeenCalled();
  });

  it('o campo de busca tem nome acessível, papel de combobox e recebe o foco ao abrir', async () => {
    const f = montar('Empresa');
    await f.whenStable();

    expect(campo(f).getAttribute('aria-label')).toBe('Buscar tela');
    expect(campo(f).getAttribute('role')).toBe('combobox');
    expect(document.activeElement).toBe(campo(f));
  });
});
