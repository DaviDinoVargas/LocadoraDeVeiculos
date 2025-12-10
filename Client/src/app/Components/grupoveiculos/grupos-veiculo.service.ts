import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { GrupoVeiculoDto } from './grupo-veiculo.model';

@Injectable({ providedIn: 'root' })
export class GruposVeiculoService {
  private base = 'https://localhost:7064/api/grupos-automovel';

  constructor(private http: HttpClient) {}

  private extractData<T>(raw: any): T {
    if (!raw) return raw;
    if (raw.dados) return raw.dados as T;
    if (raw.registros) return raw.registros as T;
    return raw as T;
  }

  listar(): Observable<GrupoVeiculoDto[]> {
    return this.http.get<any>(this.base).pipe(map(raw => this.extractData<GrupoVeiculoDto[]>(raw)));
  }

  obter(id: string): Observable<GrupoVeiculoDto> {
    return this.http.get<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<GrupoVeiculoDto>(raw)));
  }

  criar(payload: GrupoVeiculoDto): Observable<GrupoVeiculoDto> {
    return this.http.post<any>(this.base, payload).pipe(map(raw => this.extractData<GrupoVeiculoDto>(raw)));
  }

  atualizar(id: string, payload: GrupoVeiculoDto): Observable<GrupoVeiculoDto> {
    return this.http.put<any>(`${this.base}/${id}`, payload).pipe(map(raw => this.extractData<GrupoVeiculoDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<void>(raw)));
  }
}
