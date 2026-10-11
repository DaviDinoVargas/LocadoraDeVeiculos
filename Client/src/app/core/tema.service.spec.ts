import { TestBed } from '@angular/core/testing';
import { TemaService } from './tema.service';

describe('TemaService — claro/escuro', () => {
  const raiz = document.documentElement;

  beforeEach(() => {
    raiz.classList.remove('dark');
    localStorage.removeItem('tema');
  });

  afterEach(() => {
    raiz.classList.remove('dark');
    localStorage.removeItem('tema');
  });

  it('parte do tema que o index.html já aplicou (classe .dark no <html>)', () => {
    raiz.classList.add('dark');

    expect(TestBed.inject(TemaService).tema()).toBe('escuro');
  });

  it('alternar troca a classe .dark na hora e lembra a escolha', () => {
    const servico = TestBed.inject(TemaService);
    expect(servico.tema()).toBe('claro');

    servico.alternar();
    expect(servico.tema()).toBe('escuro');
    expect(raiz.classList.contains('dark')).toBeTrue();
    expect(localStorage.getItem('tema')).toBe('escuro');

    servico.alternar();
    expect(servico.tema()).toBe('claro');
    expect(raiz.classList.contains('dark')).toBeFalse();
    expect(localStorage.getItem('tema')).toBe('claro');
  });
});
