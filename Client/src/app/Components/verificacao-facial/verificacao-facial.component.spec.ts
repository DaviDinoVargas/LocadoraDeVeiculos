import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { ConfirmacaoService } from '../../core/confirmacao.service';
import { WebcamCaptureService } from '../../shared/webcam-capture.service';
import { VerificacaoFacialComponent } from './verificacao-facial.component';

describe('VerificacaoFacialComponent', () => {
  let fixture: ComponentFixture<VerificacaoFacialComponent>;
  let auth: jasmine.SpyObj<AuthService>;
  let confirmacao: jasmine.SpyObj<ConfirmacaoService>;

  const botao = (texto: string): HTMLButtonElement | undefined =>
    Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('button'))
      .find(b => b.textContent?.trim() === texto);

  function criar(cadastrado: boolean) {
    auth.statusRosto.and.returnValue(of({ cadastrado }));
    fixture = TestBed.createComponent(VerificacaoFacialComponent);
    fixture.detectChanges();
  }

  beforeEach(() => {
    auth = jasmine.createSpyObj<AuthService>('AuthService', ['statusRosto', 'cadastrarRosto', 'verificarRosto', 'removerRosto']);
    confirmacao = jasmine.createSpyObj<ConfirmacaoService>('ConfirmacaoService', ['confirmar']);

    TestBed.configureTestingModule({
      imports: [VerificacaoFacialComponent],
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: ConfirmacaoService, useValue: confirmacao },
        { provide: WebcamCaptureService, useValue: jasmine.createSpyObj('WebcamCaptureService', ['ligar', 'desligar', 'capturarFrameBase64']) }
      ]
    });
  });

  it('consulta o status pela Web API (não pelo serviço de ML) e mostra o botão de apagar quando há rosto', () => {
    criar(true);

    expect(auth.statusRosto).toHaveBeenCalled();
    expect(fixture.componentInstance.cadastrado()).toBeTrue();
    expect(botao('Apagar meu rosto')).toBeDefined();
  });

  it('esconde "Apagar meu rosto" quando não há rosto cadastrado', () => {
    criar(false);

    expect(botao('Apagar meu rosto')).toBeUndefined();
  });

  it('apaga o rosto depois de confirmar e volta ao estado "nenhum rosto cadastrado"', () => {
    criar(true);
    confirmacao.confirmar.and.returnValue(of(true));
    auth.removerRosto.and.returnValue(of(undefined));

    botao('Apagar meu rosto')!.click();
    fixture.detectChanges();

    expect(confirmacao.confirmar).toHaveBeenCalledWith(jasmine.objectContaining({ destrutivo: true }));
    expect(auth.removerRosto).toHaveBeenCalledTimes(1);
    expect(fixture.componentInstance.cadastrado()).toBeFalse();
    expect(fixture.componentInstance.mensagemTipo()).toBe('sucesso');
    expect(botao('Apagar meu rosto')).toBeUndefined();
  });

  it('não apaga nada se o usuário cancelar a confirmação', () => {
    criar(true);
    confirmacao.confirmar.and.returnValue(of(false));

    botao('Apagar meu rosto')!.click();

    expect(auth.removerRosto).not.toHaveBeenCalled();
    expect(fixture.componentInstance.cadastrado()).toBeTrue();
  });

  it('mantém o rosto como cadastrado e mostra o erro quando o servidor falha', () => {
    criar(true);
    confirmacao.confirmar.and.returnValue(of(true));
    auth.removerRosto.and.returnValue(throwError(() => ({ status: 400, error: ['Não foi possível apagar o rosto agora.'] })));

    botao('Apagar meu rosto')!.click();

    expect(fixture.componentInstance.cadastrado()).toBeTrue();
    expect(fixture.componentInstance.mensagemTipo()).toBe('erro');
    expect(fixture.componentInstance.mensagem()).toBe('Não foi possível apagar o rosto agora.');
  });
});
