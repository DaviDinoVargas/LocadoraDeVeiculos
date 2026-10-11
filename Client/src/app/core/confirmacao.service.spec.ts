import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MatDialog } from '@angular/material/dialog';
import { ConfirmacaoService } from './confirmacao.service';

describe('ConfirmacaoService — substitui o confirm() nativo', () => {
  let service: ConfirmacaoService;
  let app: ApplicationRef;

  const overlay = () => document.querySelector('.cdk-overlay-container') as HTMLElement;
  const botao = (texto: string) =>
    Array.from(overlay().querySelectorAll('button')).find(b => b.textContent?.trim() === texto) as HTMLButtonElement;

  // afterClosed() emite depois do ciclo de fechamento do diálogo; espera assentar antes de conferir.
  const aguardar = async () => {
    app.tick();
    await new Promise(r => setTimeout(r, 30));
  };

  const abrir = (...args: Parameters<ConfirmacaoService['confirmar']>) => {
    let resultado: boolean | undefined;
    service.confirmar(...args).subscribe(r => (resultado = r));
    app.tick();
    return () => resultado;
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideNoopAnimations()] });
    service = TestBed.inject(ConfirmacaoService);
    app = TestBed.inject(ApplicationRef);
  });

  afterEach(() => TestBed.inject(MatDialog).closeAll());

  it('mostra título, mensagem e rótulos de ação específicos', () => {
    abrir({ titulo: 'Confirmar exclusão', mensagem: 'Deseja realmente excluir este veículo?', confirmar: 'Excluir' });

    expect(overlay().textContent).toContain('Confirmar exclusão');
    expect(overlay().textContent).toContain('Deseja realmente excluir este veículo?');
    expect(botao('Excluir')).toBeDefined();
    expect(botao('Cancelar')).toBeDefined();
  });

  it('ação destrutiva: botão vermelho e aviso de que não dá para desfazer', () => {
    abrir({ mensagem: 'Excluir?', confirmar: 'Excluir', destrutivo: true });

    expect(botao('Excluir').classList).toContain('btn-danger');
    expect(overlay().textContent).toContain('Esta ação não pode ser desfeita.');
  });

  it('ação comum não é vermelha nem avisa de irreversibilidade', () => {
    abrir({ mensagem: 'Iniciar?', confirmar: 'Iniciar aluguel' });

    expect(botao('Iniciar aluguel').classList).toContain('btn-primary');
    expect(overlay().textContent).not.toContain('não pode ser desfeita');
  });

  it('clicar em confirmar emite true', async () => {
    const resultado = abrir({ mensagem: 'Excluir?', confirmar: 'Excluir' });

    botao('Excluir').click();
    await aguardar();

    expect(resultado()).toBeTrue();
  });

  it('clicar em cancelar emite false', async () => {
    const resultado = abrir({ mensagem: 'Excluir?', confirmar: 'Excluir' });

    botao('Cancelar').click();
    await aguardar();

    expect(resultado()).toBeFalse();
  });

  it('fechar o diálogo sem escolher (Esc / clique fora) conta como false', async () => {
    const resultado = abrir('Excluir?');

    TestBed.inject(MatDialog).closeAll();
    await aguardar();

    expect(resultado()).toBeFalse();
  });

  it('aceita só o texto da mensagem e usa rótulos padrão', () => {
    abrir('Tem certeza?');

    expect(overlay().textContent).toContain('Tem certeza?');
    expect(overlay().textContent).toContain('Confirmar ação');
    expect(botao('Confirmar')).toBeDefined();
  });

  it('o foco inicial fica no botão seguro (Cancelar)', async () => {
    abrir({ mensagem: 'Excluir?', confirmar: 'Excluir', destrutivo: true });
    await new Promise(r => setTimeout(r, 80));

    expect(document.activeElement).toBe(botao('Cancelar'));
  });
});
