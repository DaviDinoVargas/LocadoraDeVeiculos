import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { FuncionarioDto } from './funcionario.model';

@Injectable({ providedIn: 'root' })
export class FuncionariosService {
  // CORRIGIR: Mude de 7043 para 7064
  private base = 'https://localhost:7064/api/funcionarios';

  constructor(private http: HttpClient) {}

  private extractData<T>(raw: any): T {
    if (!raw) return raw;
    if (raw.dados) return raw.dados as T;
    if (raw.registros) return raw.registros as T;
    return raw as T;
  }

  listar(): Observable<FuncionarioDto[]> {
    return this.http.get<any>(this.base).pipe(map(raw => this.extractData<FuncionarioDto[]>(raw)));
  }

  obter(id: string): Observable<FuncionarioDto> {
    return this.http.get<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<FuncionarioDto>(raw)));
  }

  criar(payload: FuncionarioDto): Observable<FuncionarioDto> {
    return this.http.post<any>(this.base, payload).pipe(map(raw => this.extractData<FuncionarioDto>(raw)));
  }

  atualizar(id: string, payload: FuncionarioDto): Observable<FuncionarioDto> {
    return this.http.put<any>(`${this.base}/${id}`, payload).pipe(map(raw => this.extractData<FuncionarioDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<void>(raw)));
  }
}
