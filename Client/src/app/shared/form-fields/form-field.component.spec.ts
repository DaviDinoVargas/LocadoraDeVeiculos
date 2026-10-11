import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { FormFieldComponent, mensagemDeErro } from './form-field.component';

@Component({
  imports: [ReactiveFormsModule, HlmInputImports, FormFieldComponent],
  template: `
    <app-form-field label="E-mail" required hint="Usado para entrar" [mensagens]="{ email: 'Esse e-mail não parece certo' }">
      <input hlmInput type="email" [formControl]="email" />
    </app-form-field>
  `
})
class HostComponent {
  email = new FormControl('', [Validators.required, Validators.email]);
}

describe('mensagemDeErro', () => {
  it('traduz os erros padrão com texto específico', () => {
    expect(mensagemDeErro({ required: true })).toBe('Campo obrigatório');
    expect(mensagemDeErro({ email: true })).toBe('Informe um e-mail válido');
    expect(mensagemDeErro({ minlength: { requiredLength: 6, actualLength: 2 } })).toBe('Use ao menos 6 caracteres');
    expect(mensagemDeErro({ min: { min: 1, actual: 0 } })).toBe('O valor mínimo é 1');
    expect(mensagemDeErro({ cpfInvalido: true })).toBe('CPF inválido');
  });

  it('mensagens do campo sobrepõem as padrão e erro do servidor chega como texto', () => {
    expect(mensagemDeErro({ required: true }, { required: 'Informe o nome' })).toBe('Informe o nome');
    expect(mensagemDeErro({ servidor: 'CPF já cadastrado.' })).toBe('CPF já cadastrado.');
  });

  it('sem erro ou erro desconhecido', () => {
    expect(mensagemDeErro(null)).toBe('');
    expect(mensagemDeErro({ outro: true })).toBe('Valor inválido');
  });
});

describe('FormFieldComponent — rótulo, ajuda e erro ligados ao campo', () => {
  let fixture: ComponentFixture<HostComponent>;
  const el = () => fixture.nativeElement as HTMLElement;
  const input = () => el().querySelector('input') as HTMLInputElement;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('o rótulo fica visível acima e é de fato o rótulo do campo (for ↔ id)', () => {
    const rotulo = el().querySelector('label') as HTMLLabelElement;

    expect(rotulo.textContent).toContain('E-mail');
    expect(rotulo.textContent).toContain('(obrigatório)');
    expect(rotulo.htmlFor).toBe(input().id);
    expect(input().labels?.length).toBe(1);
  });

  it('mostra a ajuda; o erro só aparece depois que o campo foi tocado', () => {
    expect(el().textContent).toContain('Usado para entrar');
    expect(el().querySelector('[role="alert"]')).toBeNull();

    input().dispatchEvent(new Event('blur'));
    fixture.detectChanges();

    expect(el().querySelector('[role="alert"]')?.textContent).toContain('Campo obrigatório');
    expect(input().getAttribute('aria-invalid')).toBe('true');
  });

  it('usa a mensagem específica do campo e o erro aponta para o input por aria-describedby', () => {
    input().value = 'isso-nao-e-email';
    input().dispatchEvent(new Event('input'));
    input().dispatchEvent(new Event('blur'));
    fixture.detectChanges();

    const erro = el().querySelector('[role="alert"]') as HTMLElement;
    expect(erro.textContent).toContain('Esse e-mail não parece certo');
    expect(input().getAttribute('aria-describedby')).toContain(erro.id);
  });
});
