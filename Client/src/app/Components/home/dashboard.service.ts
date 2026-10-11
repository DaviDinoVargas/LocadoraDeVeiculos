import { Injectable } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/api.config';
import { SILENCIAR_ERRO } from '../../core/http-context';

// Resposta de GET /api/dashboard/resumo (números agregados calculados no servidor)
export interface ResumoDashboardDto {
  frota: { total: number; emUso: number; disponiveis: number };
  alugueis: { emAndamento: number; reservados: number; atrasados: number };
  financeiro: { receitaDoMes: number; receitaMesAnterior: number; devolucoesDoMes: number };
  totalClientes: number;
  ultimosMeses: { ano: number; mes: number; alugueis: number; receita: number }[];
  geradoEm: string;
}

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private base = `${API_BASE_URL}/dashboard`;

  constructor(private http: HttpClient) {}

  // Falha tratada na própria Home (estado de erro com "Tentar novamente"), sem snackbar.
  resumo(): Observable<ResumoDashboardDto> {
    return this.http.get<ResumoDashboardDto>(`${this.base}/resumo`, {
      context: new HttpContext().set(SILENCIAR_ERRO, true)
    });
  }
}
