import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { map, catchError } from 'rxjs/operators';

export interface AccessToken {
  accessToken: string;
  expires: string; // ISO string
  usuario?: Usuario;
}

export interface Usuario {
  id: string;
  nomeCompleto: string;
  email: string;
  cargo?: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private baseUrl = 'https://localhost:7064/api/auth';

  // Estado de sessão mantido só em memória (nunca em localStorage/sessionStorage):
  // um XSS que rode no site não consegue mais roubar o token lendo o storage do navegador.
  // O preço disso é que um F5 apaga o access token — por isso existe tentarRestaurarSessao(),
  // que usa o refresh token (cookie HttpOnly, inacessível a JS) para obter um novo sem novo login.
  private accessToken: string | null = null;
  private expires: string | null = null;
  private usuario: Usuario | null = null;

  constructor(private http: HttpClient) {}

  /** LOGIN */
  autenticar(email: string, senha: string): Observable<AccessToken> {
    return this.http.post<any>(`${this.baseUrl}/autenticar`, { email, senha }, { withCredentials: true })
      .pipe(
        map(response => this.mapTokenResponse(response)),
        catchError(err => throwError(() => err))
      );
  }

  /** REGISTRO */
  registrar(nomeCompleto: string, email: string, senha: string, confirmarSenha: string): Observable<AccessToken> {
    return this.http.post<any>(`${this.baseUrl}/registrar`, { nomeCompleto, email, senha, confirmarSenha }, { withCredentials: true })
      .pipe(
        map(response => this.mapTokenResponse(response)),
        catchError(err => throwError(() => err))
      );
  }

  /** ROTACIONAR TOKEN */
  rotacionarToken(): Observable<AccessToken> {
    const token = this.getAccessToken();
    return this.http.post<any>(`${this.baseUrl}/rotacionar`, {}, {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined,
      withCredentials: true
    }).pipe(
      map(response => this.mapTokenResponse(response)),
      catchError(err => throwError(() => err))
    );
  }

  /** LOGOUT */
  sair(): Observable<void> {
    const token = this.getAccessToken();
    return this.http.post<void>(`${this.baseUrl}/sair`, {}, {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined,
      withCredentials: true
    }).pipe(
      map(() => this.limparStorage()),
      catchError(err => {
        this.limparStorage(); // garante logout mesmo se o backend falhar
        return throwError(() => err);
      })
    );
  }

  /** GUARDA O TOKEN EM MEMÓRIA (nunca em storage do navegador) */
  private salvarToken(token: AccessToken): void {
    if (!token || !token.accessToken) return;
    this.accessToken = token.accessToken;
    this.expires = token.expires || new Date(Date.now() + 3600 * 1000).toISOString();
    this.usuario = token.usuario ?? null;
  }

  /** LIMPA A SESSÃO EM MEMÓRIA */
  public limparStorage(): void {
    this.accessToken = null;
    this.expires = null;
    this.usuario = null;
  }

  /**
   * Tenta restaurar a sessão após um F5/nova aba, quando o access token em memória já
   * se perdeu. Usa o refresh token (cookie HttpOnly) para pedir um novo access token
   * sem precisar pedir login/senha de novo. Retorna false se não havia sessão válida.
   */
  tentarRestaurarSessao(): Observable<boolean> {
    return this.rotacionarToken().pipe(
      map(() => true),
      catchError(() => {
        this.limparStorage();
        return of(false);
      })
    );
  }

  /** MAPEAR RESPOSTA DO BACKEND PARA AccessToken */
  private mapTokenResponse(response: any): AccessToken {
    if (!response) throw new Error('Resposta inválida do servidor');

    const tokenResponse: AccessToken = {
      accessToken: response.chave,
      expires: response.expiracao,
      usuario: response.usuarioAutenticado
    };

    this.salvarToken(tokenResponse);
    return tokenResponse;
  }

  /** GETTERS */
  getAccessToken(): string | null {
    return this.accessToken;
  }

  getUsuario(): Usuario | null {
    return this.usuario;
  }

  getExpires(): string | null {
    return this.expires;
  }

  /** VALIDAÇÕES */
  isTokenExpirado(): boolean {
    const expires = this.getExpires();
    if (!expires) return true;
    return Date.now() >= new Date(expires).getTime();
  }

  isLoggedIn(): boolean {
    const token = this.getAccessToken();
    return !!token && !this.isTokenExpirado();
  }
}
