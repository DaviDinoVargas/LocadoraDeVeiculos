import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { ConfiguracaoDto } from './configuracao.model';
import { API_BASE_URL } from '../../core/api.config';
import { extrairDados } from '../../core/api-response';

@Injectable({ providedIn: 'root' })
export class ConfiguracoesService {
  private base = `${API_BASE_URL}/configuracoes`;

  constructor(private http: HttpClient) {}


  obter(): Observable<ConfiguracaoDto> {
    return this.http.get<unknown>(this.base).pipe(map(raw => extrairDados<ConfiguracaoDto>(raw)));
  }

  atualizarValorGarantia(valor: number): Observable<ConfiguracaoDto> {
    return this.http.put<unknown>(`${this.base}/valor-garantia`, { valorGarantia: valor })
      .pipe(map(raw => extrairDados<ConfiguracaoDto>(raw)));
  }

  atualizarPrecoCombustivel(preco: number): Observable<ConfiguracaoDto> {
    return this.http.put<unknown>(`${this.base}/preco-combustivel`, { precoCombustivel: preco })
      .pipe(map(raw => extrairDados<ConfiguracaoDto>(raw)));
  }
}
