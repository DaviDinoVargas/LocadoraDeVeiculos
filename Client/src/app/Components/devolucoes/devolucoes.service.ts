import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { DevolucaoDto, DevolucaoCompletoDto, SelecionarDevolucoesDto } from './devolucao.model';
import { API_BASE_URL } from '../../core/api.config';
import { extrairDados } from '../../core/api-response';

@Injectable({ providedIn: 'root' })
export class DevolucoesService {
  private base = `${API_BASE_URL}/devolucoes`;

  constructor(private http: HttpClient) {}


  listar(): Observable<SelecionarDevolucoesDto[]> {
    return this.http.get<unknown>(this.base).pipe(map(raw => extrairDados<SelecionarDevolucoesDto[]>(raw)));
  }

  obter(id: string): Observable<DevolucaoCompletoDto> {
    return this.http.get<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<DevolucaoCompletoDto>(raw)));
  }

  obterPorAluguelId(aluguelId: string): Observable<DevolucaoCompletoDto> {
    return this.http.get<unknown>(`${this.base}/aluguel/${aluguelId}`).pipe(map(raw => extrairDados<DevolucaoCompletoDto>(raw)));
  }

  registrar(payload: DevolucaoDto): Observable<DevolucaoDto> {
    return this.http.post<unknown>(this.base, payload).pipe(map(raw => extrairDados<DevolucaoDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<void>(raw)));
  }
}
