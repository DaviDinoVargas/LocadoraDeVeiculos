import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { Observable, of } from 'rxjs';
import { ConfirmacaoService } from './confirmacao.service';
import { formularioSemPerdaGuard } from './form-guard';

describe('formularioSemPerdaGuard — alterações não salvas', () => {
  const executar = (form: FormGroup) =>
    TestBed.runInInjectionContext(() =>
      formularioSemPerdaGuard({ form }, {} as ActivatedRouteSnapshot, {} as RouterStateSnapshot, {} as RouterStateSnapshot)
    );

  let confirmar: jasmine.Spy;

  beforeEach(() => {
    confirmar = jasmine.createSpy('confirmar').and.returnValue(of(false));
    TestBed.configureTestingModule({ providers: [{ provide: ConfirmacaoService, useValue: { confirmar } }] });
  });

  it('formulário sem alterações sai direto, sem perguntar', () => {
    expect(executar(new FormGroup({ nome: new FormControl('') }))).toBeTrue();
    expect(confirmar).not.toHaveBeenCalled();
  });

  it('com alterações pergunta com ação destrutiva e respeita a resposta', () => {
    const form = new FormGroup({ nome: new FormControl('') });
    form.controls['nome'].setValue('x');
    form.markAsDirty();

    let resposta: boolean | undefined;
    (executar(form) as Observable<boolean>).subscribe(r => (resposta = r));

    expect(confirmar).toHaveBeenCalledWith(jasmine.objectContaining({ destrutivo: true, avisoIrreversivel: false, confirmar: 'Descartar e sair' }));
    expect(resposta).toBeFalse();
  });

  it('depois de salvar (markAsPristine) não pergunta de novo', () => {
    const form = new FormGroup({ nome: new FormControl('') });
    form.markAsDirty();
    form.markAsPristine();

    expect(executar(form)).toBeTrue();
  });
});
