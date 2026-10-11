import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { ConfiguracaoDto } from './configuracao.model';
import { API_BASE_URL } from '../../core/api.config';

@Injectable({ providedIn: 'root' })
export class ConfiguracoesService {
  private base = `${API_BASE_URL}/configuracoes`;

  constructor(private http: HttpClient) {}

  private extractData<T>(raw: any): T {
    if (!raw) return raw;
    if (raw.dados) return raw.dados as T;
    if (raw.registros) return raw.registros as T;
    return raw as T;
  }

  obter(): Observable<ConfiguracaoDto> {
    return this.http.get<any>(this.base).pipe(map(raw => this.extractData<ConfiguracaoDto>(raw)));
  }

  atualizarPrecoCombustivel(preco: number): Observable<ConfiguracaoDto> {
    return this.http.put<any>(`${this.base}/preco-combustivel`, { precoCombustivel: preco })
      .pipe(map(raw => this.extractData<ConfiguracaoDto>(raw)));
  }
}
