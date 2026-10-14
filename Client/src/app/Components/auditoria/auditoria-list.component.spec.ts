import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuditoriaListComponent } from './auditoria-list.component';
import { RegistroAuditoria } from './auditoria.model';
import { AuditoriaService } from './auditoria.service';
import { violacoesNosDoisTemas } from '../../core/testing/acessibilidade';

describe('AuditoriaListComponent (S14)', () => {
  let svc: jasmine.SpyObj<AuditoriaService>;

  const registro = (extra: Partial<RegistroAuditoria>): RegistroAuditoria => ({
    id: crypto.randomUUID(),
    emUtc: '2026-10-13T15:30:00',
    usuarioId: 'u1',
    acao: 'LoginOk',
    entidade: 'Autenticacao',
    entidadeId: null,
    camposAlterados: null,
    enderecoIp: '203.0.113.7',
    ...extra
  });

  const montar = () => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: AuditoriaService, useValue: svc }] });
    const fixture = TestBed.createComponent(AuditoriaListComponent);
    fixture.detectChanges();
    return fixture;
  };

  beforeEach(() => {
    svc = jasmine.createSpyObj('AuditoriaService', ['listar']);
  });

  it('carrega os registros e mostra ação traduzida, entidade e IP', () => {
    svc.listar.and.returnValue(of([
      registro({ acao: 'LoginFalhou' }),
      registro({ acao: 'Alterado', entidade: 'Cliente', camposAlterados: 'Nome,Telefone', enderecoIp: null })
    ]));

    const f = montar();
    const texto = f.nativeElement.textContent as string;

    expect(svc.listar).toHaveBeenCalledTimes(1);
    expect(f.componentInstance.registros().length).toBe(2);
    expect(texto).toContain('Falha de login');
    expect(texto).toContain('Cliente');
    expect(texto).not.toContain('LoginFalhou');
    expect(f.nativeElement.querySelectorAll('tbody tr').length).toBe(2);
  });

  it('lista vazia mostra o estado vazio, sem erro', () => {
    svc.listar.and.returnValue(of([]));

    const f = montar();

    expect(f.nativeElement.textContent).toContain('Nenhum registro ainda');
    expect(f.componentInstance.erro()).toBeFalse();
  });

  it('falha de carregamento mostra o estado de erro e permite tentar de novo', () => {
    svc.listar.and.returnValue(throwError(() => new Error('falhou')));
    const f = montar();
    expect(f.componentInstance.erro()).toBeTrue();
    expect(f.componentInstance.carregando()).toBeFalse();

    svc.listar.and.returnValue(of([registro({})]));
    f.componentInstance.carregar();

    expect(f.componentInstance.erro()).toBeFalse();
    expect(f.componentInstance.registros().length).toBe(1);
  });

  it('o horário sem fuso vindo da API é tratado como UTC', () => {
    svc.listar.and.returnValue(of([registro({ emUtc: '2026-10-13T15:30:00' })]));
    const f = montar();
    const coluna = f.componentInstance.colunas.find(c => c.id === 'emUtc')!;

    expect(coluna.formatar!(f.componentInstance.registros()[0])).toBe(new Date('2026-10-13T15:30:00Z').toLocaleString('pt-BR'));
  });

  it('acessibilidade (axe-core, WCAG 2.2 AA): sem violações nos temas claro e escuro', async () => {
    svc.listar.and.returnValue(of([registro({ acao: 'LoginFalhou' }), registro({ acao: 'Alterado', entidade: 'Cliente', camposAlterados: 'Nome,Telefone' })]));
    const fixture = montar();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(await violacoesNosDoisTemas(fixture.nativeElement)).toEqual([]);
  });
});
