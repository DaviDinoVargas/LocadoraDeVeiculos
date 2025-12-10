import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { DevolucaoDto, DevolucaoCompletoDto, SelecionarDevolucoesDto } from './devolucao.model';

@Injectable({ providedIn: 'root' })
export class DevolucoesService {
  private base = 'https://localhost:7064/api/devolucoes';

  constructor(private http: HttpClient) {}

  private extractData<T>(raw: any): T {
    if (!raw) return raw;
    if (raw.dados) return raw.dados as T;
    if (raw.registros) return raw.registros as T;
    return raw as T;
  }

  listar(): Observable<SelecionarDevolucoesDto[]> {
    return this.http.get<any>(this.base).pipe(map(raw => this.extractData<SelecionarDevolucoesDto[]>(raw)));
  }

  obter(id: string): Observable<DevolucaoCompletoDto> {
    return this.http.get<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<DevolucaoCompletoDto>(raw)));
  }

  obterPorAluguelId(aluguelId: string): Observable<DevolucaoCompletoDto> {
    return this.http.get<any>(`${this.base}/aluguel/${aluguelId}`).pipe(map(raw => this.extractData<DevolucaoCompletoDto>(raw)));
  }

  registrar(payload: DevolucaoDto): Observable<DevolucaoDto> {
    return this.http.post<any>(this.base, payload).pipe(map(raw => this.extractData<DevolucaoDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<void>(raw)));
  }
}
