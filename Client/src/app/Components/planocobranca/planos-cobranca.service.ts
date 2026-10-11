import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { PlanoCobrancaDto } from './plano-cobranca.model';
import { API_BASE_URL } from '../../core/api.config';
import { extrairDados } from '../../core/api-response';

@Injectable({ providedIn: 'root' })
export class PlanosCobrancaService {
  private base = `${API_BASE_URL}/planos-cobranca`;

  constructor(private http: HttpClient) {}


  listar(): Observable<PlanoCobrancaDto[]> {
    return this.http.get<unknown>(this.base).pipe(map(raw => extrairDados<PlanoCobrancaDto[]>(raw)));
  }

  obter(id: string): Observable<PlanoCobrancaDto> {
    return this.http.get<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<PlanoCobrancaDto>(raw)));
  }

  criar(payload: PlanoCobrancaDto): Observable<PlanoCobrancaDto> {
    return this.http.post<unknown>(this.base, payload).pipe(map(raw => extrairDados<PlanoCobrancaDto>(raw)));
  }

  atualizar(id: string, payload: PlanoCobrancaDto): Observable<PlanoCobrancaDto> {
    return this.http.put<unknown>(`${this.base}/${id}`, payload).pipe(map(raw => extrairDados<PlanoCobrancaDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<void>(raw)));
  }
}
