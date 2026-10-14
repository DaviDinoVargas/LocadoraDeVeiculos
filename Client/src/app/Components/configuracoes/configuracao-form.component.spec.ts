import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NotificacaoService } from '../../core/notificacao.service';
import { criarNotificacaoFalsa } from '../../core/testing/notificacao-fake.spec';
import { of } from 'rxjs';
import { ConfiguracaoFormComponent } from './configuracao-form.component';
import { ConfiguracoesService } from './configuracoes.service';
import { violacoesNosDoisTemas } from '../../core/testing/acessibilidade';

describe('ConfiguracaoFormComponent — caução configurável', () => {
  let fixture: ComponentFixture<ConfiguracaoFormComponent>;
  let svc: jasmine.SpyObj<ConfiguracoesService>;
  let chamadas: string[];

  beforeEach(async () => {
    chamadas = [];
    svc = jasmine.createSpyObj('ConfiguracoesService', ['obter', 'atualizarPrecoCombustivel', 'atualizarValorGarantia']);
    svc.obter.and.returnValue(of({ id: '1', precoCombustivel: 5.5, valorGarantia: 1000 }));
    svc.atualizarPrecoCombustivel.and.callFake(() => { chamadas.push('preco'); return of({ precoCombustivel: 6, valorGarantia: 1000 }); });
    svc.atualizarValorGarantia.and.callFake(() => { chamadas.push('garantia'); return of({ precoCombustivel: 5.5, valorGarantia: 2500 }); });

    await TestBed.configureTestingModule({
      imports: [ConfiguracaoFormComponent],
      providers: [provideRouter([]), { provide: ConfiguracoesService, useValue: svc }, { provide: NotificacaoService, useValue: criarNotificacaoFalsa() }]
    }).compileComponents();

    fixture = TestBed.createComponent(ConfiguracaoFormComponent);
    fixture.detectChanges();
  });

  it('carrega a caução vigente no campo (antes era um 1000 fixo e desabilitado)', () => {
    expect(fixture.componentInstance.form.get('valorGarantia')?.value).toBe(1000);
    const campo = fixture.nativeElement.querySelector('input[formControlName="valorGarantia"]') as HTMLInputElement;
    expect(campo.disabled).toBeFalse();
  });

  it('salva só a caução quando só ela mudou', () => {
    fixture.componentInstance.form.patchValue({ valorGarantia: 2500 });
    fixture.componentInstance.salvar();

    expect(chamadas).toEqual(['garantia']);
  });

  it('salva preço e caução em sequência quando os dois mudaram', () => {
    fixture.componentInstance.form.patchValue({ precoCombustivel: 6, valorGarantia: 2500 });
    fixture.componentInstance.salvar();

    expect(chamadas).toEqual(['preco', 'garantia']);
  });

  it('não chama a API quando nada mudou', () => {
    fixture.componentInstance.salvar();

    expect(chamadas).toEqual([]);
    expect(fixture.componentInstance.mensagemSucesso()).toContain('sucesso');
  });

  it('rejeita caução negativa ou acima do limite', () => {
    const campo = fixture.componentInstance.form.get('valorGarantia')!;
    campo.setValue(-1);
    expect(campo.valid).toBeFalse();
    campo.setValue(100001);
    expect(campo.valid).toBeFalse();
    campo.setValue(0);
    expect(campo.valid).toBeTrue();
  });

  it('acessibilidade (axe-core, WCAG 2.2 AA): sem violações nos temas claro e escuro', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(await violacoesNosDoisTemas(fixture.nativeElement)).toEqual([]);
  });
});
