import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { NotificacaoService } from '../../core/notificacao.service';
import { criarNotificacaoFalsa } from '../../core/testing/notificacao-fake.spec';
import { FuncionarioFormComponent } from './funcionario-form.component';
import { FuncionarioDto } from './funcionario.model';
import { FuncionariosService } from './funcionarios.service';

describe('FuncionarioFormComponent — senha no cadastro e na edição', () => {
  let fixture: ComponentFixture<FuncionarioFormComponent>;
  let component: FuncionarioFormComponent;
  let svc: jasmine.SpyObj<FuncionariosService>;

  const preenchido = { nomeCompleto: 'Ana Souza', cpf: '529.982.247-25', email: 'ana@exemplo.com', salario: 2500, admissaoEmUtc: '2026-01-10' };

  const montar = (id: string | null) => {
    svc = jasmine.createSpyObj('FuncionariosService', ['obter', 'criar', 'atualizar']);
    svc.obter.and.returnValue(of({ id: 'f1', ...preenchido, admissaoEmUtc: '2026-01-10T00:00:00Z' } as FuncionarioDto));
    svc.criar.and.returnValue(of({} as FuncionarioDto));
    svc.atualizar.and.returnValue(of({} as FuncionarioDto));
    const params = new BehaviorSubject(convertToParamMap(id ? { id } : {}));

    TestBed.configureTestingModule({
      imports: [FuncionarioFormComponent],
      providers: [
        provideRouter([]),
        { provide: FuncionariosService, useValue: svc },
        { provide: NotificacaoService, useValue: criarNotificacaoFalsa() },
        { provide: ActivatedRoute, useValue: { paramMap: params, snapshot: { paramMap: params.value } } }
      ]
    });
    spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(FuncionarioFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  };

  it('cadastro: a senha é obrigatória e precisa de 6 caracteres', () => {
    montar(null);
    component.form.patchValue(preenchido);

    expect(component.form.valid).toBeFalse();
    component.form.patchValue({ senha: '123', confirmarSenha: '123' });
    expect(component.form.controls.senha.hasError('minlength')).toBeTrue();
    component.form.patchValue({ senha: '123456', confirmarSenha: '123456' });
    expect(component.form.valid).toBeTrue();
  });

  it('o erro "senhas diferentes" fica no campo de confirmação', () => {
    montar(null);
    component.form.patchValue({ ...preenchido, senha: '123456', confirmarSenha: '654321' });

    expect(component.form.controls.confirmarSenha.hasError('mismatch')).toBeTrue();
    expect(component.form.valid).toBeFalse();

    component.form.patchValue({ confirmarSenha: '123456' });
    expect(component.form.controls.confirmarSenha.hasError('mismatch')).toBeFalse();
  });

  it('edição: senha em branco é permitida e NÃO vai no envio (mantém a atual)', () => {
    montar('f1');

    expect(component.form.controls.senha.hasError('required')).toBeFalse();
    component.salvar();

    const payload = svc.atualizar.calls.mostRecent().args[1];
    expect(payload.senha).toBeUndefined();
    expect(payload.confirmarSenha).toBeUndefined();
  });

  it('edição: nunca preenche as senhas e mostra a admissão no formato do input date', () => {
    montar('f1');

    expect(component.form.controls.senha.value).toBe('');
    expect(component.form.controls.admissaoEmUtc.value).toBe('2026-01-10');
  });

  it('edição: uma nova senha digitada é enviada', () => {
    montar('f1');
    component.form.patchValue({ senha: 'nova-senha', confirmarSenha: 'nova-senha' });

    component.salvar();

    expect(svc.atualizar.calls.mostRecent().args[1].senha).toBe('nova-senha');
  });
});
