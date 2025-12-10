import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { CondutorDto } from './condutor.model';

@Injectable({ providedIn: 'root' })
export class CondutoresService {
  private base = 'https://localhost:7064/api/condutores';

  constructor(private http: HttpClient) {}

  private extractData<T>(raw: any): T {
    if (!raw) return raw;
    if (raw.dados) return raw.dados as T;
    if (raw.registros) return raw.registros as T;
    return raw as T;
  }

  listar(): Observable<CondutorDto[]> {
    return this.http.get<any>(this.base).pipe(map(raw => this.extractData<CondutorDto[]>(raw)));
  }

  listarPorCliente(clienteId: string): Observable<CondutorDto[]> {
    return this.http.get<any>(`${this.base}/cliente/${clienteId}`).pipe(map(raw => this.extractData<CondutorDto[]>(raw)));
  }

  obter(id: string): Observable<CondutorDto> {
    return this.http.get<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<CondutorDto>(raw)));
  }

  criar(payload: CondutorDto): Observable<CondutorDto> {
    return this.http.post<any>(this.base, payload).pipe(map(raw => this.extractData<CondutorDto>(raw)));
  }

  atualizar(id: string, payload: CondutorDto): Observable<CondutorDto> {
    return this.http.put<any>(`${this.base}/${id}`, payload).pipe(map(raw => this.extractData<CondutorDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<void>(raw)));
  }
}
