import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { PlanoCobrancaDto } from './plano-cobranca.model';

@Injectable({ providedIn: 'root' })
export class PlanosCobrancaService {
  private base = 'https://localhost:7064/api/planos-cobranca';

  constructor(private http: HttpClient) {}

  private extractData<T>(raw: any): T {
    if (!raw) return raw;
    if (raw.dados) return raw.dados as T;
    if (raw.registros) return raw.registros as T;
    return raw as T;
  }

  listar(): Observable<PlanoCobrancaDto[]> {
    return this.http.get<any>(this.base).pipe(map(raw => this.extractData<PlanoCobrancaDto[]>(raw)));
  }

  obter(id: string): Observable<PlanoCobrancaDto> {
    return this.http.get<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<PlanoCobrancaDto>(raw)));
  }

  criar(payload: PlanoCobrancaDto): Observable<PlanoCobrancaDto> {
    return this.http.post<any>(this.base, payload).pipe(map(raw => this.extractData<PlanoCobrancaDto>(raw)));
  }

  atualizar(id: string, payload: PlanoCobrancaDto): Observable<PlanoCobrancaDto> {
    return this.http.put<any>(`${this.base}/${id}`, payload).pipe(map(raw => this.extractData<PlanoCobrancaDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<void>(raw)));
  }
}
