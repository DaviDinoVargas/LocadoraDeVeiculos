import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { AluguelDto, AluguelCompletoDto, SelecionarAlugueisDto, SimulacaoAluguelDto, SimularAluguelRequest } from './aluguel.model';
import { SILENCIAR_ERRO } from '../../core/http-context';
import { API_BASE_URL } from '../../core/api.config';
import { extrairDados } from '../../core/api-response';


@Injectable({ providedIn: 'root' })
export class AlugueisService {
  private base = `${API_BASE_URL}/alugueis`;

  private readonly http = inject(HttpClient);


  listar(): Observable<SelecionarAlugueisDto[]> {
    return this.http.get<unknown>(this.base).pipe(map(raw => extrairDados<SelecionarAlugueisDto[]>(raw)));
  }

  listarPorStatus(status: string): Observable<SelecionarAlugueisDto[]> {
    return this.http.get<unknown>(`${this.base}/status/${status}`).pipe(map(raw => extrairDados<SelecionarAlugueisDto[]>(raw)));
  }


    selecionarEmAberto(): Observable<SelecionarAlugueisDto[]> {
    return this.http.get<unknown>(`${this.base}/em-aberto`).pipe(
      map(raw => extrairDados<SelecionarAlugueisDto[]>(raw))
    );
  }

  obter(id: string): Observable<AluguelCompletoDto> {
    return this.http.get<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<AluguelCompletoDto>(raw)));
  }

  // Chamada a cada mudança de campo do formulário: o erro é mostrado no próprio painel de valores,
  // então o interceptor global não deve abrir um snackbar a cada falha.
  simular(payload: SimularAluguelRequest): Observable<SimulacaoAluguelDto> {
    return this.http.post<SimulacaoAluguelDto>(`${this.base}/simular`, payload, {
      context: new HttpContext().set(SILENCIAR_ERRO, true)
    });
  }

  criar(payload: AluguelDto): Observable<AluguelDto> {
    return this.http.post<unknown>(this.base, payload).pipe(map(raw => extrairDados<AluguelDto>(raw)));
  }

  atualizar(id: string, payload: AluguelDto): Observable<AluguelDto> {
    return this.http.put<unknown>(`${this.base}/${id}`, payload).pipe(map(raw => extrairDados<AluguelDto>(raw)));
  }

  iniciar(id: string): Observable<{ id: string; status: string }> {
    return this.http.post<unknown>(`${this.base}/${id}/iniciar`, {}).pipe(map(raw => extrairDados<{ id: string; status: string }>(raw)));
  }

  cancelar(id: string): Observable<{ id: string; status: string }> {
    return this.http.post<unknown>(`${this.base}/${id}/cancelar`, {}).pipe(map(raw => extrairDados<{ id: string; status: string }>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<void>(raw)));
  }

}

