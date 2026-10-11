import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { ClientePessoaFisicaFormComponent } from './ClientePessoaFisicaFormComponent';
import { ClientePessoaJuridicaFormComponent } from './ClientePessoaJuridicaFormComponent';
import { ClientesService } from './clientes.service';
import { CondutorFormComponent } from '../condutor/condutor-form.component';
import { CondutoresService } from '../condutor/condutores.service';
import { FuncionarioFormComponent } from '../funcionarios/funcionario-form.component';
import { FuncionariosService } from '../funcionarios/funcionarios.service';
import { ParceiroFormComponent } from '../parceiros/parceiro-form.component';
import { ParceirosService } from '../parceiros/parceiros.service';

// Os cinco formulários que pedem CPF/CNPJ passam a usar os mesmos validadores (dígito verificador)
// e a mesma máscara de digitação. Antes: só regex de formato, e só o formulário de pessoa física
// tinha máscara.
describe('Formulários com CPF/CNPJ — validação de dígitos e máscara', () => {
  const stub = { listar: () => of([]), listarPessoasJuridicas: () => of([]), obter: () => of({}) };

  function montar<T>(componente: new (...args: never[]) => T, servico: unknown, stubServico: object = stub) {
    TestBed.configureTestingModule({
      imports: [componente as never],
      providers: [
        provideRouter([]),
        { provide: ClientesService, useValue: stub }, // o formulário de condutor também lista clientes
        { provide: servico as never, useValue: stubServico },
        { provide: MatSnackBar, useValue: { open: () => {} } }
      ]
    });
    const fixture = TestBed.createComponent(componente as never) as unknown as import('@angular/core/testing').ComponentFixture<T & { form: import('@angular/forms').FormGroup }>;
    fixture.detectChanges();
    return fixture;
  }

  const digitar = (fixture: { nativeElement: HTMLElement; detectChanges(): void }, campo: string, texto: string) => {
    const input = fixture.nativeElement.querySelector(`input[formControlName="${campo}"]`) as HTMLInputElement;
    input.value = texto;
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('blur'));
    fixture.detectChanges();
    return input;
  };

  const casos: { nome: string; componente: new (...args: never[]) => unknown; servico: unknown; campo: 'cpf' | 'cnpj'; valido: string; digitos: string }[] = [
    { nome: 'Cliente PF', componente: ClientePessoaFisicaFormComponent, servico: ClientesService, campo: 'cpf', valido: '529.982.247-25', digitos: '52998224725' },
    { nome: 'Cliente PJ', componente: ClientePessoaJuridicaFormComponent, servico: ClientesService, campo: 'cnpj', valido: '11.222.333/0001-81', digitos: '11222333000181' },
    { nome: 'Condutor', componente: CondutorFormComponent, servico: CondutoresService, campo: 'cpf', valido: '529.982.247-25', digitos: '52998224725' },
    { nome: 'Funcionário', componente: FuncionarioFormComponent, servico: FuncionariosService, campo: 'cpf', valido: '529.982.247-25', digitos: '52998224725' },
    { nome: 'Parceiro', componente: ParceiroFormComponent, servico: ParceirosService, campo: 'cnpj', valido: '11.222.333/0001-81', digitos: '11222333000181' }
  ];

  for (const c of casos) {
    describe(c.nome, () => {
      it(`mascara o ${c.campo.toUpperCase()} enquanto digita e guarda o valor formatado`, () => {
        const fixture = montar(c.componente, c.servico);

        const input = digitar(fixture, c.campo, c.digitos);

        expect(input.value).toBe(c.valido);
        expect(fixture.componentInstance.form.get(c.campo)?.value).toBe(c.valido);
      });

      it('aceita o número válido', () => {
        const fixture = montar(c.componente, c.servico);

        digitar(fixture, c.campo, c.digitos);

        expect(fixture.componentInstance.form.get(c.campo)?.errors).toBeNull();
      });

      it('recusa dígito verificador errado e mostra "inválido: confira os números"', () => {
        const fixture = montar(c.componente, c.servico);
        const errado = c.digitos.slice(0, -1) + (c.digitos.endsWith('0') ? '1' : '0');

        digitar(fixture, c.campo, errado);

        expect(fixture.componentInstance.form.get(c.campo)?.hasError(`${c.campo}Invalido`)).toBeTrue();
        expect((fixture.nativeElement as HTMLElement).textContent).toContain('inválido: confira os números');
      });

      it('campo vazio dá só "obrigatório"', () => {
        const fixture = montar(c.componente, c.servico);

        digitar(fixture, c.campo, '');

        const ctrl = fixture.componentInstance.form.get(c.campo)!;
        expect(ctrl.hasError('required')).toBeTrue();
        expect(ctrl.hasError(`${c.campo}Invalido`)).toBeFalse();
        expect((fixture.nativeElement as HTMLElement).textContent).toContain('obrigatório');
      });
    });
  }
});
