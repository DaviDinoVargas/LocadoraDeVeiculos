import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { aplicarMascara, mascararCnpj, mascararCpf, MascaraDirective } from './mascara.directive';

describe('funções de máscara', () => {
  it('CPF: aplica pontuação progressivamente e limita a 11 dígitos', () => {
    expect(mascararCpf('')).toBe('');
    expect(mascararCpf('529')).toBe('529');
    expect(mascararCpf('5299')).toBe('529.9');
    expect(mascararCpf('529982')).toBe('529.982');
    expect(mascararCpf('5299822')).toBe('529.982.2');
    expect(mascararCpf('529982247')).toBe('529.982.247');
    expect(mascararCpf('5299822472')).toBe('529.982.247-2');
    expect(mascararCpf('52998224725')).toBe('529.982.247-25');
    expect(mascararCpf('5299822472599')).toBe('529.982.247-25');
  });

  it('CNPJ: aplica pontuação progressivamente e limita a 14 dígitos', () => {
    expect(mascararCnpj('11')).toBe('11');
    expect(mascararCnpj('112')).toBe('11.2');
    expect(mascararCnpj('11222333')).toBe('11.222.333');
    expect(mascararCnpj('112223330')).toBe('11.222.333/0');
    expect(mascararCnpj('11222333000181')).toBe('11.222.333/0001-81');
    expect(mascararCnpj('1122233300018199')).toBe('11.222.333/0001-81');
  });

  it('ignora letras e símbolos e reaproveita valores já formatados', () => {
    expect(mascararCpf('abc529.982.247-25')).toBe('529.982.247-25');
    expect(aplicarMascara('cnpj', '11.222.333/0001-81')).toBe('11.222.333/0001-81');
    expect(mascararCpf(null)).toBe('');
  });
});

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, MascaraDirective],
  template: `<input id="cpf" [formControl]="cpf" appMascara="cpf" /><input id="cnpj" [formControl]="cnpj" appMascara="cnpj" />`
})
class HostComponent {
  cpf = new FormControl('');
  cnpj = new FormControl('');
}

describe('MascaraDirective', () => {
  let fixture: ComponentFixture<HostComponent>;

  const digitar = (id: string, texto: string): HTMLInputElement => {
    const input = fixture.nativeElement.querySelector(`#${id}`) as HTMLInputElement;
    input.value = texto;
    input.dispatchEvent(new Event('input'));
    return input;
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  });

  it('mascara o que é digitado e guarda o valor MASCARADO no formulário (formato que a API exige)', () => {
    const input = digitar('cpf', '52998224725');

    expect(input.value).toBe('529.982.247-25');
    expect(fixture.componentInstance.cpf.value).toBe('529.982.247-25');
  });

  it('funciona digito a digito', () => {
    let input = digitar('cpf', '5');
    input = digitar('cpf', '5299');

    expect(input.value).toBe('529.9');
    expect(fixture.componentInstance.cpf.value).toBe('529.9');
  });

  it('mascara CNPJ', () => {
    const input = digitar('cnpj', '11222333000181');

    expect(input.value).toBe('11.222.333/0001-81');
    expect(fixture.componentInstance.cnpj.value).toBe('11.222.333/0001-81');
  });

  it('descarta letras digitadas e respeita o tamanho máximo do campo', () => {
    const cpf = digitar('cpf', '52a99b8224725xyz99');

    expect(cpf.value).toBe('529.982.247-25');
    expect(cpf.getAttribute('maxlength')).toBe('14');
    expect(fixture.nativeElement.querySelector('#cnpj').getAttribute('maxlength')).toBe('18');
    expect(cpf.getAttribute('inputmode')).toBe('numeric');
  });
});
