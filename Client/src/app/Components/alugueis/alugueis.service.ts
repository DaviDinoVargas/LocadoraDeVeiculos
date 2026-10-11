import { Injectable } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { AluguelDto, AluguelCompletoDto, SelecionarAlugueisDto, SimulacaoAluguelDto, SimularAluguelRequest } from './aluguel.model';
import { SILENCIAR_ERRO } from '../../core/http-context';
import { API_BASE_URL } from '../../core/api.config';


@Injectable({ providedIn: 'root' })
export class AlugueisService {
  private base = `${API_BASE_URL}/alugueis`;

  constructor(private http: HttpClient) {}

  private extractData<T>(raw: any): T {
    if (!raw) return raw;
    if (raw.dados) return raw.dados as T;
    if (raw.registros) return raw.registros as T;
    return raw as T;
  }

  listar(): Observable<SelecionarAlugueisDto[]> {
    return this.http.get<any>(this.base).pipe(map(raw => this.extractData<SelecionarAlugueisDto[]>(raw)));
  }

  listarPorStatus(status: string): Observable<SelecionarAlugueisDto[]> {
    return this.http.get<any>(`${this.base}/status/${status}`).pipe(map(raw => this.extractData<SelecionarAlugueisDto[]>(raw)));
  }


    selecionarEmAberto(): Observable<SelecionarAlugueisDto[]> {
    return this.http.get<any>(`${this.base}/em-aberto`).pipe(
      map(raw => this.extractData<SelecionarAlugueisDto[]>(raw))
    );
  }

  obter(id: string): Observable<AluguelCompletoDto> {
    return this.http.get<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<AluguelCompletoDto>(raw)));
  }

  // Chamada a cada mudança de campo do formulário: o erro é mostrado no próprio painel de valores,
  // então o interceptor global não deve abrir um snackbar a cada falha.
  simular(payload: SimularAluguelRequest): Observable<SimulacaoAluguelDto> {
    return this.http.post<SimulacaoAluguelDto>(`${this.base}/simular`, payload, {
      context: new HttpContext().set(SILENCIAR_ERRO, true)
    });
  }

  criar(payload: AluguelDto): Observable<AluguelDto> {
    return this.http.post<any>(this.base, payload).pipe(map(raw => this.extractData<AluguelDto>(raw)));
  }

  atualizar(id: string, payload: AluguelDto): Observable<AluguelDto> {
    return this.http.put<any>(`${this.base}/${id}`, payload).pipe(map(raw => this.extractData<AluguelDto>(raw)));
  }

  iniciar(id: string): Observable<any> {
    return this.http.post<any>(`${this.base}/${id}/iniciar`, {}).pipe(map(raw => this.extractData<any>(raw)));
  }

  cancelar(id: string): Observable<any> {
    return this.http.post<any>(`${this.base}/${id}/cancelar`, {}).pipe(map(raw => this.extractData<any>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<void>(raw)));
  }

}

