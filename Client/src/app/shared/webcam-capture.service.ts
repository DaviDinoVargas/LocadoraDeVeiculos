import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class WebcamCaptureService {
  private stream: MediaStream | null = null;

  async ligar(video: HTMLVideoElement): Promise<void> {
    this.stream = await navigator.mediaDevices.getUserMedia({ video: { width: 480, height: 360 }, audio: false });
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
