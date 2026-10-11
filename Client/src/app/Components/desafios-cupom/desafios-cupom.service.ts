import { Injectable } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { DesafioCupomDto, ProgressoDesafioDto } from './desafio-cupom.model';
import { API_BASE_URL } from '../../core/api.config';
import { SILENCIAR_ERRO } from '../../core/http-context';
import { extrairDados } from '../../core/api-response';

@Injectable({ providedIn: 'root' })
export class DesafiosCupomService {
  private base = `${API_BASE_URL}/desafios-cupom`;

  constructor(private http: HttpClient) {}


  listar(): Observable<DesafioCupomDto[]> {
    return this.http.get<unknown>(this.base).pipe(map(raw => extrairDados<DesafioCupomDto[]>(raw)));
  }

  obter(id: string): Observable<DesafioCupomDto> {
    return this.http.get<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<DesafioCupomDto>(raw)));
  }

  criar(payload: DesafioCupomDto): Observable<DesafioCupomDto> {
    return this.http.post<unknown>(this.base, payload).pipe(map(raw => extrairDados<DesafioCupomDto>(raw)));
  }

  atualizar(id: string, payload: DesafioCupomDto): Observable<DesafioCupomDto> {
    return this.http.put<unknown>(`${this.base}/${id}`, payload).pipe(map(raw => extrairDados<DesafioCupomDto>(raw)));
  }

  // Metas ativas do cliente e quanto ele já cumpriu (alugueis concluídos no período do desafio).
  progressoDoCliente(clienteId: string): Observable<ProgressoDesafioDto[]> {
    return this.http.get<{ progresso: ProgressoDesafioDto[] }>(`${this.base}/cliente/${clienteId}/progresso`, {
      context: new HttpContext().set(SILENCIAR_ERRO, true)
    }).pipe(map(raw => raw?.progresso ?? []));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<void>(raw)));
  }
}
