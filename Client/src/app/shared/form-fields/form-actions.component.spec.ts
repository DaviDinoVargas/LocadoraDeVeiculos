import { TestBed } from '@angular/core/testing';
import { FormActionsComponent } from './form-actions.component';

describe('FormActionsComponent — barra de ações do formulário', () => {
  const criar = () => {
    TestBed.configureTestingModule({ imports: [FormActionsComponent] });
    const fixture = TestBed.createComponent(FormActionsComponent);
    fixture.detectChanges();
    return fixture;
  };

  it('fica na base da área de conteúdo: sticky, empurrada para o rodapé e compensando o padding do <main>', () => {
    const host = criar().nativeElement as HTMLElement;

    // mt-auto: em formulário curto a barra desce até o rodapé em vez de ficar no meio da tela.
    // -bottom-6: o sticky mede a partir da borda interna do <main> (que tem py-6); sem isso a barra
    // pararia 24 px acima do fim e deixaria o conteúdo aparecer por baixo.
    for (const classe of ['sticky', '-bottom-6', 'mt-auto']) {
      expect(host.classList).withContext(classe).toContain(classe);
    }
    expect(host.classList).not.toContain('bottom-0');
  });

  it('mostra o aviso só quando há alterações e usa o rótulo informado', () => {
    const fixture = criar();
    const texto = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(texto()).not.toContain('Alterações não salvas');
    expect(texto()).toContain('Salvar');

    fixture.componentRef.setInput('alterado', true);
    fixture.componentRef.setInput('rotuloSalvar', 'Criar grupo');
    fixture.detectChanges();

    expect(texto()).toContain('Alterações não salvas');
    expect(texto()).toContain('Criar grupo');
  });

  it('emite cancelar e bloqueia os botões enquanto salva', () => {
    const fixture = criar();
    const botoes = () => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));
    let cancelados = 0;
    fixture.componentInstance.cancelar.subscribe(() => cancelados++);

    botoes()[0].click();
    expect(cancelados).toBe(1);

    fixture.componentRef.setInput('salvando', true);
    fixture.detectChanges();

    expect(botoes().every(b => b.disabled)).toBeTrue();
    expect(botoes()[1].getAttribute('aria-busy')).toBe('true');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Salvando');
  });
});
