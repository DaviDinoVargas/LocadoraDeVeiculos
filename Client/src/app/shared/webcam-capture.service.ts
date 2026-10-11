import { Injectable } from '@angular/core';

export type MotivoFalhaCamera = 'permissao' | 'em-uso' | 'inexistente' | 'demora' | 'desconhecido';

/** Falha ao ligar a webcam, já com uma mensagem que diz ao usuário o que fazer. */
export class WebcamErro extends Error {
  constructor(readonly motivo: MotivoFalhaCamera, mensagem: string) {
    super(mensagem);
  }
}

// Se a câmera não responder em tanto tempo, avisa em vez de deixar "Ligando câmera…" para sempre.
const LIMITE_LIGAR_MS = 8000;

const MENSAGENS: Record<MotivoFalhaCamera, string> = {
  permissao: 'O navegador bloqueou a câmera. Libere a permissão de câmera para este site e tente de novo.',
  'em-uso': 'A câmera está em uso por outro programa (por exemplo, o Monitoramento de câmeras aberto em outra aba). Feche-o e tente de novo.',
  inexistente: 'Nenhuma câmera foi encontrada neste computador.',
  demora: 'A câmera está demorando para responder. Ela pode estar em uso por outro programa (como o Monitoramento de câmeras): feche-o e tente de novo.',
  desconhecido: 'Não foi possível acessar a câmera. Verifique as permissões do navegador.'
};

export function motivoDoErro(erro: unknown): MotivoFalhaCamera {
  switch ((erro as { name?: string } | null)?.name) {
    case 'NotAllowedError':
    case 'SecurityError':
      return 'permissao';
    case 'NotReadableError':
    case 'TrackStartError':
    case 'AbortError':
      return 'em-uso';
    case 'NotFoundError':
    case 'OverconstrainedError':
      return 'inexistente';
    default:
      return 'desconhecido';
  }
}

@Injectable({ providedIn: 'root' })
export class WebcamCaptureService {
  private stream: MediaStream | null = null;

  /**
   * Liga a webcam e a mostra no <video>. Falhas viram WebcamErro (permissão, câmera em uso, sem
   * câmera ou demora). Pede resolução "ideal" (não exata) de 640x480 em câmera frontal: o driver
   * escolhe o modo que já tem, sem renegociar formato, o que costuma abrir mais rápido que
   * forçar 480x360.
   */
  async ligar(video: HTMLVideoElement): Promise<void> {
    this.desligar();

    let temporizador: ReturnType<typeof setTimeout> | undefined;
    let expirou = false;

    const pedido = navigator.mediaDevices.getUserMedia({
      video: { width: { ideal: 640 }, height: { ideal: 480 }, facingMode: 'user' },
      audio: false
    });

    // getUserMedia não é cancelável: se a demora estourar e a câmera responder depois, solta o stream.
    pedido.then(stream => {
      if (expirou) {
        stream.getTracks().forEach(track => track.stop());
      }
    }, () => undefined);

    const limite = new Promise<never>((_, rejeitar) => {
      temporizador = setTimeout(() => {
        expirou = true;
        rejeitar(new WebcamErro('demora', MENSAGENS.demora));
      }, LIMITE_LIGAR_MS);
    });

    try {
      this.stream = await Promise.race([pedido, limite]);
    } catch (erro) {
      if (erro instanceof WebcamErro) {
        throw erro;
      }
      const motivo = motivoDoErro(erro);
      throw new WebcamErro(motivo, MENSAGENS[motivo]);
    } finally {
      clearTimeout(temporizador);
    }

    video.srcObject = this.stream;
    await video.play();
  }

  desligar(): void {
    this.stream?.getTracks().forEach(track => track.stop());
    this.stream = null;
  }

  capturarFrameBase64(video: HTMLVideoElement, canvas: HTMLCanvasElement): string | null {
    canvas.width = video.videoWidth || 480;
    canvas.height = video.videoHeight || 360;
    const ctx = canvas.getContext('2d');
    if (!ctx) {
      return null;
    }
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
    return canvas.toDataURL('image/jpeg', 0.85);
  }
}
