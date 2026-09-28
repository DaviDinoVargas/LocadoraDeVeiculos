import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { CupomDto } from './cupom.model';

@Injectable({ providedIn: 'root' })
export class CuponsService {
  private base = 'https://localhost:7064/api/cupons';

  constructor(private http: HttpClient) {}

  private extractData<T>(raw: any): T {
    if (!raw) return raw;
    if (raw.registros) return raw.registros as T;
    return raw as T;
  }

  listar(): Observable<CupomDto[]> {
    return this.http.get<any>(this.base).pipe(map(raw => this.extractData<CupomDto[]>(raw)));
  }

  obter(id: string): Observable<CupomDto> {
    return this.http.get<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<CupomDto>(raw)));
  }

  criar(payload: CupomDto): Observable<CupomDto> {
    return this.http.post<any>(this.base, payload).pipe(map(raw => this.extractData<CupomDto>(raw)));
  }

  atualizar(id: string, payload: CupomDto): Observable<CupomDto> {
    return this.http.put<any>(`${this.base}/${id}`, payload).pipe(map(raw => this.extractData<CupomDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<void>(raw)));
  }
}
