import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { TaxaServicoDto } from './taxa-servico.model';
import { API_BASE_URL } from '../../core/api.config';
import { extrairDados } from '../../core/api-response';

@Injectable({ providedIn: 'root' })
export class TaxasServicosService {
  private base = `${API_BASE_URL}/taxas-servicos`;

  constructor(private http: HttpClient) {}


  listar(): Observable<TaxaServicoDto[]> {
    return this.http.get<unknown>(this.base).pipe(map(raw => extrairDados<TaxaServicoDto[]>(raw)));
  }

  listarPorTipo(tipo: string): Observable<TaxaServicoDto[]> {
    return this.http.get<unknown>(`${this.base}/tipo/${tipo}`).pipe(map(raw => extrairDados<TaxaServicoDto[]>(raw)));
  }

  obter(id: string): Observable<TaxaServicoDto> {
    return this.http.get<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<TaxaServicoDto>(raw)));
  }

  criar(payload: TaxaServicoDto): Observable<TaxaServicoDto> {
    return this.http.post<unknown>(this.base, payload).pipe(map(raw => extrairDados<TaxaServicoDto>(raw)));
  }

  atualizar(id: string, payload: TaxaServicoDto): Observable<TaxaServicoDto> {
    return this.http.put<unknown>(`${this.base}/${id}`, payload).pipe(map(raw => extrairDados<TaxaServicoDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<void>(raw)));
  }
}
