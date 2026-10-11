import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { ParceiroDto } from './parceiro.model';
import { API_BASE_URL } from '../../core/api.config';
import { extrairDados } from '../../core/api-response';

@Injectable({ providedIn: 'root' })
export class ParceirosService {
  private base = `${API_BASE_URL}/parceiros`;

  constructor(private http: HttpClient) {}


  listar(): Observable<ParceiroDto[]> {
    return this.http.get<unknown>(this.base).pipe(map(raw => extrairDados<ParceiroDto[]>(raw)));
  }

  obter(id: string): Observable<ParceiroDto> {
    return this.http.get<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<ParceiroDto>(raw)));
  }

  criar(payload: ParceiroDto): Observable<ParceiroDto> {
    return this.http.post<unknown>(this.base, payload).pipe(map(raw => extrairDados<ParceiroDto>(raw)));
  }

  atualizar(id: string, payload: ParceiroDto): Observable<ParceiroDto> {
    return this.http.put<unknown>(`${this.base}/${id}`, payload).pipe(map(raw => extrairDados<ParceiroDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<void>(raw)));
  }
}
