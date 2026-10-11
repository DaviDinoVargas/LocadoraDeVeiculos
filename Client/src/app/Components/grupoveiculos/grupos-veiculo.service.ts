import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { GrupoVeiculoDto } from './grupo-veiculo.model';
import { API_BASE_URL } from '../../core/api.config';
import { extrairDados } from '../../core/api-response';

@Injectable({ providedIn: 'root' })
export class GruposVeiculoService {
  private base = `${API_BASE_URL}/grupos-automovel`;

  private readonly http = inject(HttpClient);


  listar(): Observable<GrupoVeiculoDto[]> {
    return this.http.get<unknown>(this.base).pipe(map(raw => extrairDados<GrupoVeiculoDto[]>(raw)));
  }

  obter(id: string): Observable<GrupoVeiculoDto> {
    return this.http.get<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<GrupoVeiculoDto>(raw)));
  }

  criar(payload: GrupoVeiculoDto): Observable<GrupoVeiculoDto> {
    return this.http.post<unknown>(this.base, payload).pipe(map(raw => extrairDados<GrupoVeiculoDto>(raw)));
  }

  atualizar(id: string, payload: GrupoVeiculoDto): Observable<GrupoVeiculoDto> {
    return this.http.put<unknown>(`${this.base}/${id}`, payload).pipe(map(raw => extrairDados<GrupoVeiculoDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<void>(raw)));
  }
}
