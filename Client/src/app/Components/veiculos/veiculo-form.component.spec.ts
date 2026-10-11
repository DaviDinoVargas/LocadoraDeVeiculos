import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { NotificacaoService } from '../../core/notificacao.service';
import { criarNotificacaoFalsa } from '../../core/testing/notificacao-fake.spec';
import { GruposVeiculoService } from '../grupoveiculos/grupos-veiculo.service';
import { VeiculoFormComponent } from './veiculo-form.component';
import { VeiculosService } from './veiculos.service';
import { VeiculoDto } from './veiculo.model';

describe('VeiculoFormComponent — placa e contrato com a API', () => {
  let fixture: ComponentFixture<VeiculoFormComponent>;
  let component: VeiculoFormComponent;
  let veiculos: jasmine.SpyObj<VeiculosService>;

  const montar = (id: string | null) => {
    veiculos = jasmine.createSpyObj('VeiculosService', ['obter', 'criar', 'atualizar']);
    veiculos.obter.and.returnValue(of({ id: 'v1', placa: 'ABC1D30', marca: 'Fiat', modelo: 'Uno', cor: 'Prata', ano: 2020, tipoCombustivel: 'Flex', capacidadeTanque: 50, grupoAutomovelId: 'g1' } as VeiculoDto));
    veiculos.criar.and.returnValue(of({} as VeiculoDto));
    veiculos.atualizar.and.returnValue(of({} as VeiculoDto));
    const params = new BehaviorSubject(convertToParamMap(id ? { id } : {}));

    TestBed.configureTestingModule({
      imports: [VeiculoFormComponent],
      providers: [
        provideRouter([]),
        { provide: VeiculosService, useValue: veiculos },
        { provide: GruposVeiculoService, useValue: { listar: () => of([{ id: 'g1', nome: 'Econômico' }]) } },
        { provide: NotificacaoService, useValue: criarNotificacaoFalsa() },
        { provide: ActivatedRoute, useValue: { paramMap: params, snapshot: { paramMap: params.value } } }
      ]
    });
    spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(VeiculoFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  };

  const digitar = (campo: string, texto: string) => {
    const input = fixture.nativeElement.querySelector(`input[formControlName="${campo}"]`) as HTMLInputElement;
    input.value = texto;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    return input;
  };

  it('a máscara formata a placa enquanto digita (ABC-1D23)', () => {
    montar(null);

    const input = digitar('placa', 'abc1d23');

    expect(input.value).toBe('ABC-1D23');
    expect(component.form.controls.placa.valid).toBeTrue();
  });

  it('envia a placa SEM hífen, como a API exige (7 caracteres)', () => {
    montar(null);
    component.form.patchValue({
      placa: 'ABC-1D23', marca: 'Fiat', modelo: 'Uno', cor: 'Prata', ano: 2020,
      tipoCombustivel: 'Flex', capacidadeTanque: 50, grupoAutomovelId: 'g1'
    });

    component.salvar();

    expect(veiculos.criar.calls.mostRecent().args[0].placa).toBe('ABC1D23');
  });

  it('na edição mostra a placa que a API devolve (sem hífen) já com a máscara, e o formulário fica válido', () => {
    montar('v1');

    expect(component.form.controls.placa.value).toBe('ABC-1D30');
    expect(component.form.valid).toBeTrue();
    expect(component.form.dirty).toBeFalse();
  });

  it('na edição salva com atualizar() e a placa sem hífen', () => {
    montar('v1');

    component.salvar();

    expect(veiculos.atualizar).toHaveBeenCalledOnceWith('v1', jasmine.objectContaining({ placa: 'ABC1D30' }));
  });

  it('placa fora do formato mostra a mensagem específica', () => {
    montar(null);

    const input = digitar('placa', 'abc');
    input.dispatchEvent(new Event('blur'));
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Placa inválida. Use o formato AAA-0000 ou AAA-0A00.');
  });
});
