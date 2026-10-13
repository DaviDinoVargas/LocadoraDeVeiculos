import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL } from '../../core/api.config';
import { extrairDados } from '../../core/api-response';
import { RegistroAuditoria } from './auditoria.model';

@Injectable({ providedIn: 'root' })
export class AuditoriaService {
  private readonly http = inject(HttpClient);

  /** Registros mais recentes da empresa do usuário (a API limita a 500). */
  listar(limite = 200): Observable<RegistroAuditoria[]> {
    return this.http
      .get<unknown>(`${API_BASE_URL}/auditoria`, { params: { limite } })
      .pipe(map(raw => extrairDados<RegistroAuditoria[]>(raw)));
  }
}
