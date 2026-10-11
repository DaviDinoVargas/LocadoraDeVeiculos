import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { FuncionarioDto } from './funcionario.model';
import { API_BASE_URL } from '../../core/api.config';
import { extrairDados } from '../../core/api-response';

@Injectable({ providedIn: 'root' })
export class FuncionariosService {
  // CORRIGIR: Mude de 7043 para 7064
  private base = `${API_BASE_URL}/funcionarios`;

  constructor(private http: HttpClient) {}


  listar(): Observable<FuncionarioDto[]> {
    return this.http.get<unknown>(this.base).pipe(map(raw => extrairDados<FuncionarioDto[]>(raw)));
  }

  obter(id: string): Observable<FuncionarioDto> {
    return this.http.get<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<FuncionarioDto>(raw)));
  }

  criar(payload: FuncionarioDto): Observable<FuncionarioDto> {
    return this.http.post<unknown>(this.base, payload).pipe(map(raw => extrairDados<FuncionarioDto>(raw)));
  }

  atualizar(id: string, payload: FuncionarioDto): Observable<FuncionarioDto> {
    return this.http.put<unknown>(`${this.base}/${id}`, payload).pipe(map(raw => extrairDados<FuncionarioDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<void>(raw)));
  }
}
