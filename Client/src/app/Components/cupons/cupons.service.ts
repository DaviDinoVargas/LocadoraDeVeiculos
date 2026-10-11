import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { CupomDto, ValidacaoCupomDto } from './cupom.model';
import { API_BASE_URL } from '../../core/api.config';
import { SILENCIAR_ERRO } from '../../core/http-context';
import { extrairDados } from '../../core/api-response';

@Injectable({ providedIn: 'root' })
export class CuponsService {
  private base = `${API_BASE_URL}/cupons`;

  private readonly http = inject(HttpClient);


  listar(): Observable<CupomDto[]> {
    return this.http.get<unknown>(this.base).pipe(map(raw => extrairDados<CupomDto[]>(raw)));
  }

  obter(id: string): Observable<CupomDto> {
    return this.http.get<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<CupomDto>(raw)));
  }

  criar(payload: CupomDto): Observable<CupomDto> {
    return this.http.post<unknown>(this.base, payload).pipe(map(raw => extrairDados<CupomDto>(raw)));
  }

  atualizar(id: string, payload: CupomDto): Observable<CupomDto> {
    return this.http.put<unknown>(`${this.base}/${id}`, payload).pipe(map(raw => extrairDados<CupomDto>(raw)));
  }

  // Usada enquanto o usuário digita o código no formulário de aluguel: o resultado (válido ou não)
  // aparece no próprio formulário, então erros HTTP não abrem snackbar a cada tentativa.
  validar(codigo: string, valorBase: number): Observable<ValidacaoCupomDto> {
    return this.http.post<ValidacaoCupomDto>(`${this.base}/validar`, { codigo, valorBase }, {
      context: new HttpContext().set(SILENCIAR_ERRO, true)
    });
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<void>(raw)));
  }
}
