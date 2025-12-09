// auth.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { AccessToken, Usuario } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private baseUrl = 'https://localhost:7064/api/auth';
  private accessTokenKey = 'locadora_access_token';
  private expiresKey = 'locadora_expires';
  private usuarioKey = 'locadora_usuario';

  constructor(private http: HttpClient) {}

  autenticar(email: string, senha: string): Observable<AccessToken> {
    return this.http.post<AccessToken>(`${this.baseUrl}/autenticar`, { email, senha })
      .pipe(
        map(token => {
          this.salvarToken(token);
          return token;
        })
      );
  }

  registrar(nomeCompleto: string, email: string, senha: string, confirmarSenha: string): Observable<AccessToken> {
    return this.http.post<AccessToken>(`${this.baseUrl}/registrar`, { nomeCompleto, email, senha, confirmarSenha })
      .pipe(
        map(token => {
          this.salvarToken(token);
          return token;
        })
      );
  }

  rotacionarToken(): Observable<AccessToken> {
    return this.http.post<AccessToken>(`${this.baseUrl}/rotacionar`, {})
      .pipe(
        map(token => {
          this.salvarToken(token);
          return token;
        })
      );
  }

  sair(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/sair`, {}).pipe(
      map(() => {
        this.limparStorage();
      })
    );
  }

  private salvarToken(token: AccessToken): void {
    localStorage.setItem(this.accessTokenKey, token.accessToken);
    localStorage.setItem(this.expiresKey, token.expires);
    if (token.usuario) {
      localStorage.setItem(this.usuarioKey, JSON.stringify(token.usuario));
    }
  }

  public limparStorage(): void {
    localStorage.removeItem(this.accessTokenKey);
    localStorage.removeItem(this.expiresKey);
    localStorage.removeItem(this.usuarioKey);
  }

  getAccessToken(): string | null {
    return localStorage.getItem(this.accessTokenKey);
  }

  getExpires(): string | null {
    return localStorage.getItem(this.expiresKey);
  }

  getUsuario(): Usuario | null {
    const usuarioStr = localStorage.getItem(this.usuarioKey);
    if (usuarioStr) {
      return JSON.parse(usuarioStr) as Usuario;
    }
    return null;
  }

  isTokenExpirado(): boolean {
    const expires = this.getExpires();
    if (!expires) return true;
    return Date.now() >= new Date(expires).getTime();
  }

  isLoggedIn(): boolean {
    return !!this.getAccessToken() && !this.isTokenExpirado();
  }
}
