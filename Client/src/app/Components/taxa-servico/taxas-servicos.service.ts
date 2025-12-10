import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { TaxaServicoDto } from './taxa-servico.model';

@Injectable({ providedIn: 'root' })
export class TaxasServicosService {
  private base = 'https://localhost:7064/api/taxas-servicos';

  constructor(private http: HttpClient) {}

  private extractData<T>(raw: any): T {
    if (!raw) return raw;
    if (raw.dados) return raw.dados as T;
    if (raw.registros) return raw.registros as T;
    return raw as T;
  }

  listar(): Observable<TaxaServicoDto[]> {
    return this.http.get<any>(this.base).pipe(map(raw => this.extractData<TaxaServicoDto[]>(raw)));
  }

  listarPorTipo(tipo: string): Observable<TaxaServicoDto[]> {
    return this.http.get<any>(`${this.base}/tipo/${tipo}`).pipe(map(raw => this.extractData<TaxaServicoDto[]>(raw)));
  }

  obter(id: string): Observable<TaxaServicoDto> {
    return this.http.get<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<TaxaServicoDto>(raw)));
  }

  criar(payload: TaxaServicoDto): Observable<TaxaServicoDto> {
    return this.http.post<any>(this.base, payload).pipe(map(raw => this.extractData<TaxaServicoDto>(raw)));
  }

  atualizar(id: string, payload: TaxaServicoDto): Observable<TaxaServicoDto> {
    return this.http.put<any>(`${this.base}/${id}`, payload).pipe(map(raw => this.extractData<TaxaServicoDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<void>(raw)));
  }
}
