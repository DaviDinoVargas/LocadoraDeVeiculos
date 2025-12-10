import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { VeiculoDto } from './veiculo.model';

@Injectable({ providedIn: 'root' })
export class VeiculosService {
  private base = 'https://localhost:7064/api/automoveis';

  constructor(private http: HttpClient) {}

  private extractData<T>(raw: any): T {
    if (!raw) return raw;
    if (raw.dados) return raw.dados as T;
    if (raw.registros) return raw.registros as T;
    return raw as T;
  }

  listar(): Observable<VeiculoDto[]> {
    return this.http.get<any>(this.base).pipe(map(raw => this.extractData<VeiculoDto[]>(raw)));
  }

  obter(id: string): Observable<VeiculoDto> {
    return this.http.get<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<VeiculoDto>(raw)));
  }

  criar(payload: VeiculoDto): Observable<VeiculoDto> {
    return this.http.post<any>(this.base, payload).pipe(map(raw => this.extractData<VeiculoDto>(raw)));
  }

  atualizar(id: string, payload: VeiculoDto): Observable<VeiculoDto> {
    return this.http.put<any>(`${this.base}/${id}`, payload).pipe(map(raw => this.extractData<VeiculoDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<void>(raw)));
  }

  listarPorGrupo(grupoId: string): Observable<VeiculoDto[]> {
    return this.http.get<any>(`${this.base}/grupo/${grupoId}`).pipe(map(raw => this.extractData<VeiculoDto[]>(raw)));
  }
}
