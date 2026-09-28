import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { ParceiroDto } from './parceiro.model';

@Injectable({ providedIn: 'root' })
export class ParceirosService {
  private base = 'https://localhost:7064/api/parceiros';

  constructor(private http: HttpClient) {}

  private extractData<T>(raw: any): T {
    if (!raw) return raw;
    if (raw.registros) return raw.registros as T;
    return raw as T;
  }

  listar(): Observable<ParceiroDto[]> {
    return this.http.get<any>(this.base).pipe(map(raw => this.extractData<ParceiroDto[]>(raw)));
  }

  obter(id: string): Observable<ParceiroDto> {
    return this.http.get<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<ParceiroDto>(raw)));
  }

  criar(payload: ParceiroDto): Observable<ParceiroDto> {
    return this.http.post<any>(this.base, payload).pipe(map(raw => this.extractData<ParceiroDto>(raw)));
  }

  atualizar(id: string, payload: ParceiroDto): Observable<ParceiroDto> {
    return this.http.put<any>(`${this.base}/${id}`, payload).pipe(map(raw => this.extractData<ParceiroDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<void>(raw)));
  }
}
