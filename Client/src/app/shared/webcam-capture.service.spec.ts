import { fakeAsync, tick } from '@angular/core/testing';
import { WebcamCaptureService, WebcamErro, motivoDoErro } from './webcam-capture.service';

describe('WebcamCaptureService', () => {
  let servico: WebcamCaptureService;
  let getUserMedia: jasmine.Spy;
  const video = (): HTMLVideoElement => ({ play: () => Promise.resolve() } as unknown as HTMLVideoElement);
  const fluxo = () => {
    const parar = jasmine.createSpy('stop');
    return { stream: { getTracks: () => [{ stop: parar }] } as unknown as MediaStream, parar };
  };

  beforeEach(() => {
    servico = new WebcamCaptureService();
    getUserMedia = jasmine.createSpy('getUserMedia');
    spyOnProperty(navigator, 'mediaDevices', 'get').and.returnValue({ getUserMedia } as unknown as MediaDevices);
  });

  it('pede resolução ideal (não exata) em câmera frontal, sem áudio', async () => {
    const { stream } = fluxo();
    getUserMedia.and.resolveTo(stream);
    const v = video();

    await servico.ligar(v);

    const pedido = getUserMedia.calls.mostRecent().args[0] as MediaStreamConstraints;
    expect(pedido.audio).toBeFalse();
    expect(pedido.video).toEqual({ width: { ideal: 640 }, height: { ideal: 480 }, facingMode: 'user' });
    expect(v.srcObject).toBe(stream);
  });

  it('mapeia o nome do erro do navegador para o motivo', () => {
    expect(motivoDoErro({ name: 'NotAllowedError' })).toBe('permissao');
    expect(motivoDoErro({ name: 'NotReadableError' })).toBe('em-uso');
    expect(motivoDoErro({ name: 'NotFoundError' })).toBe('inexistente');
    expect(motivoDoErro(new Error('x'))).toBe('desconhecido');
    expect(motivoDoErro(null)).toBe('desconhecido');
  });

  it('câmera em uso vira WebcamErro com mensagem que diz o que fazer', async () => {
    getUserMedia.and.rejectWith({ name: 'NotReadableError' });

    const erro = await servico.ligar(video()).catch(e => e);

    expect(erro).toBeInstanceOf(WebcamErro);
    expect((erro as WebcamErro).motivo).toBe('em-uso');
    expect((erro as WebcamErro).message).toContain('em uso');
  });

  it('avisa quando a câmera demora demais e solta o stream que chegar depois', fakeAsync(() => {
    const { stream, parar } = fluxo();
    let resolver!: (s: MediaStream) => void;
    getUserMedia.and.returnValue(new Promise<MediaStream>(r => (resolver = r)));
    let erro: unknown;

    servico.ligar(video()).catch(e => (erro = e));
    tick(8000);

    expect(erro).toBeInstanceOf(WebcamErro);
    expect((erro as WebcamErro).motivo).toBe('demora');

    resolver(stream);   // a câmera respondeu tarde demais: não pode ficar ligada em segundo plano
    tick();
    expect(parar).toHaveBeenCalled();
  }));

  it('desligar para as trilhas', async () => {
    const { stream, parar } = fluxo();
    getUserMedia.and.resolveTo(stream);
    await servico.ligar(video());

    servico.desligar();

    expect(parar).toHaveBeenCalled();
  });
});
