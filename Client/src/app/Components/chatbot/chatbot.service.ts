import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

export interface ChatMensagem {
  autor: 'usuario' | 'bot';
  texto: string;
}

@Injectable({ providedIn: 'root' })
export class ChatbotService {
  private baseUrl = '/api/ml';

  constructor(private http: HttpClient) {}

  enviarMensagem(mensagem: string): Observable<string> {
    return this.http
      .post<{ reply: string }>(`${this.baseUrl}/chat`, { message: mensagem })
      .pipe(map(resposta => resposta.reply));
  }
}
