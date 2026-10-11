import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { VeiculoDto } from './veiculo.model';
import { API_BASE_URL } from '../../core/api.config';
import { extrairDados } from '../../core/api-response';

@Injectable({ providedIn: 'root' })
export class VeiculosService {
  private base = `${API_BASE_URL}/automoveis`;

  private readonly http = inject(HttpClient);


  listar(): Observable<VeiculoDto[]> {
    return this.http.get<unknown>(this.base).pipe(map(raw => extrairDados<VeiculoDto[]>(raw)));
  }

  obter(id: string): Observable<VeiculoDto> {
    return this.http.get<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<VeiculoDto>(raw)));
  }

  criar(payload: VeiculoDto): Observable<VeiculoDto> {
    return this.http.post<unknown>(this.base, payload).pipe(map(raw => extrairDados<VeiculoDto>(raw)));
  }

  atualizar(id: string, payload: VeiculoDto): Observable<VeiculoDto> {
    return this.http.put<unknown>(`${this.base}/${id}`, payload).pipe(map(raw => extrairDados<VeiculoDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<void>(raw)));
  }

  // Veículos sem aluguel ativo no período (mesma regra do 409 ao salvar). Na edição, aluguelId
  // evita que o próprio aluguel bloqueie o veículo que ele já usa.
  listarDisponiveis(dataSaida: string, dataRetornoPrevisto: string, aluguelId?: string): Observable<VeiculoDto[]> {
    let params = new HttpParams().set('dataSaida', dataSaida).set('dataRetornoPrevisto', dataRetornoPrevisto);
    if (aluguelId) params = params.set('aluguelId', aluguelId);
    return this.http.get<unknown>(`${this.base}/disponiveis`, { params }).pipe(map(raw => extrairDados<VeiculoDto[]>(raw)));
  }

  listarPorGrupo(grupoId: string): Observable<VeiculoDto[]> {
    return this.http.get<unknown>(`${this.base}/grupo/${grupoId}`).pipe(map(raw => extrairDados<VeiculoDto[]>(raw)));
  }
}
