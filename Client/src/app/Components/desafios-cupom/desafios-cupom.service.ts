import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { DesafioCupomDto } from './desafio-cupom.model';

@Injectable({ providedIn: 'root' })
export class DesafiosCupomService {
  private base = 'https://localhost:7064/api/desafios-cupom';

  constructor(private http: HttpClient) {}

  private extractData<T>(raw: any): T {
    if (!raw) return raw;
    if (raw.registros) return raw.registros as T;
    return raw as T;
  }

  listar(): Observable<DesafioCupomDto[]> {
    return this.http.get<any>(this.base).pipe(map(raw => this.extractData<DesafioCupomDto[]>(raw)));
  }

  obter(id: string): Observable<DesafioCupomDto> {
    return this.http.get<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<DesafioCupomDto>(raw)));
  }

  criar(payload: DesafioCupomDto): Observable<DesafioCupomDto> {
    return this.http.post<any>(this.base, payload).pipe(map(raw => this.extractData<DesafioCupomDto>(raw)));
  }

  atualizar(id: string, payload: DesafioCupomDto): Observable<DesafioCupomDto> {
    return this.http.put<any>(`${this.base}/${id}`, payload).pipe(map(raw => this.extractData<DesafioCupomDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<void>(raw)));
  }
}
