import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
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
  private accessTokenKey = 'locadora_access_token';
  private expiresKey = 'locadora_expires';
  private usuarioKey = 'locadora_usuario';

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

  /** SALVA TOKEN NO LOCALSTORAGE */
  private salvarToken(token: AccessToken): void {
    if (!token || !token.accessToken) return;
    this.limparStorage();
    localStorage.setItem(this.accessTokenKey, token.accessToken);
    localStorage.setItem(this.expiresKey, token.expires || new Date(Date.now() + 3600 * 1000).toISOString());
    if (token.usuario) localStorage.setItem(this.usuarioKey, JSON.stringify(token.usuario));
  }

  /** LIMPA STORAGE */
  public limparStorage(): void {
    localStorage.removeItem(this.accessTokenKey);
    localStorage.removeItem(this.expiresKey);
    localStorage.removeItem(this.usuarioKey);
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
    return localStorage.getItem(this.accessTokenKey);
  }

  getUsuario(): Usuario | null {
    const usuarioStr = localStorage.getItem(this.usuarioKey);
    if (!usuarioStr) return null;
    try { return JSON.parse(usuarioStr); }
    catch { return null; }
  }

  getExpires(): string | null {
    return localStorage.getItem(this.expiresKey);
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
