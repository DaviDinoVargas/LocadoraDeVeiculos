import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { map, catchError, finalize, shareReplay } from 'rxjs/operators';
import { API_BASE_URL } from '../core/api.config';
import { SILENCIAR_ERRO } from '../core/http-context';

// Corpo das respostas de /auth/autenticar, /registrar, /entrar-facial e /rotacionar
export interface RespostaToken {
  chave: string;
  expiracao: string;
  usuarioAutenticado?: Usuario;
}

export interface AccessToken {
  accessToken: string;
  expires: string; // ISO string
  usuario?: Usuario;
}

export interface StatusDoisFatores {
  habilitado: boolean;
  codigosDeRecuperacaoRestantes: number;
}

export interface ConfiguracaoDoisFatores {
  chave: string;
  uri: string;
}

// Termo que o usuário precisa aceitar antes de cadastrar o rosto (texto e versão vêm do servidor).
export interface TermoDeConsentimento {
  versao: string;
  texto: string;
  retencaoEmMeses: number;
}

export interface Usuario {
  id: string;
  nomeCompleto: string;
  email: string;
  cargo?: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private baseUrl = `${API_BASE_URL}/auth`;

  // Estado de sessão mantido só em memória (nunca em localStorage/sessionStorage):
  // um XSS que rode no site não consegue mais roubar o token lendo o storage do navegador.
  // O preço disso é que um F5 apaga o access token — por isso existe tentarRestaurarSessao(),
  // que usa o refresh token (cookie HttpOnly, inacessível a JS) para obter um novo sem novo login.
  private accessToken: string | null = null;
  private expires: string | null = null;
  private usuario: Usuario | null = null;

  private readonly http = inject(HttpClient);

  /** LOGIN */
  autenticar(email: string, senha: string, codigoAutenticador?: string): Observable<AccessToken> {
    return this.http.post<RespostaToken>(`${this.baseUrl}/autenticar`, { email, senha, ...(codigoAutenticador ? { codigoAutenticador } : {}) }, { withCredentials: true })
      .pipe(
        map(response => this.mapTokenResponse(response)),
        catchError(err => throwError(() => err))
      );
  }

  /** REGISTRO */
  registrar(nomeCompleto: string, email: string, senha: string, confirmarSenha: string): Observable<AccessToken> {
    return this.http.post<RespostaToken>(`${this.baseUrl}/registrar`, { nomeCompleto, email, senha, confirmarSenha }, { withCredentials: true })
      .pipe(
        map(response => this.mapTokenResponse(response)),
        catchError(err => throwError(() => err))
      );
  }

  /** LOGIN VIA RECONHECIMENTO FACIAL */
  entrarComRosto(email: string, imagemBase64: string, codigoAutenticador?: string): Observable<AccessToken> {
    return this.http.post<RespostaToken>(`${this.baseUrl}/entrar-facial`, { email, imagemBase64, ...(codigoAutenticador ? { codigoAutenticador } : {}) }, { withCredentials: true })
      .pipe(
        map(response => this.mapTokenResponse(response)),
        catchError(err => throwError(() => err))
      );
  }

  /** TERMO DE CONSENTIMENTO DO ROSTO (texto e versão vêm do servidor: o aceite vale para a versão exibida) */
  obterTermoDeConsentimento(): Observable<TermoDeConsentimento> {
    return this.http.get<TermoDeConsentimento>(`${this.baseUrl}/rosto/consentimento`);
  }

  /** CADASTRA O ROSTO DO USUÁRIO JÁ AUTENTICADO (só vale com o aceite explícito da versão atual do termo) */
  cadastrarRosto(imagemBase64: string, versaoDoTexto: string): Observable<{ amostras: number }> {
    const token = this.getAccessToken();
    return this.http.post<{ amostras: number }>(`${this.baseUrl}/rosto`, { imagemBase64, consentimento: true, versaoDoTexto }, {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined
    });
  }

  /** O USUÁRIO LOGADO TEM ROSTO CADASTRADO? (consulta via Web API; o ML não é acessível ao navegador) */
  statusRosto(): Observable<{ cadastrado: boolean }> {
    return this.http.get<{ cadastrado: boolean }>(`${this.baseUrl}/rosto`);
  }

  /** CONFERE SE A IMAGEM É DO PRÓPRIO USUÁRIO LOGADO */
  verificarRosto(imagemBase64: string): Observable<{ confirmado: boolean; confianca: number }> {
    return this.http.post<{ confirmado: boolean; confianca: number }>(`${this.baseUrl}/rosto/verificar`, { imagemBase64 });
  }

  /** APAGA O ROSTO CADASTRADO DO USUÁRIO LOGADO (o login facial deixa de funcionar até cadastrar de novo) */
  removerRosto(): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/rosto`);
  }

  /**
   * Rotacionar e sair agem só com o cookie de refresh, então a API exige este cabeçalho
   * personalizado (defesa contra CSRF: outro site não consegue enviá-lo entre origens).
   */
  private cabecalhos(token: string | null): Record<string, string> {
    return { 'X-Locadora-Client': 'web', ...(token ? { Authorization: `Bearer ${token}` } : {}) };
  }

  /** A tela já mostra o erro no próprio formulário: sem toast repetido do interceptor. */
  private semToast(): HttpContext {
    return new HttpContext().set(SILENCIAR_ERRO, true);
  }

  // ---- Conta e segurança (S1, S18, S27). Trocar a senha e encerrar a conta derrubam TODAS as sessões no servidor:
  // aqui a sessão local também é limpa e a tela leva ao login.

  /** "Esqueci minha senha": a resposta é igual para qualquer e-mail (o servidor nunca diz se a conta existe). */
  solicitarRecuperacaoDeSenha(email: string): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/esqueci-senha`, { email }, { context: this.semToast() });
  }

  /** Define a nova senha com o token que chegou por e-mail. */
  redefinirSenha(email: string, token: string, novaSenha: string, confirmarNovaSenha: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/redefinir-senha`, { email, token, novaSenha, confirmarNovaSenha }, { context: this.semToast() });
  }

  alterarSenha(senhaAtual: string, novaSenha: string, confirmarNovaSenha: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/alterar-senha`, { senhaAtual, novaSenha, confirmarNovaSenha }, { withCredentials: true })
      .pipe(map(() => this.limparStorage()));
  }

  statusDoisFatores(): Observable<StatusDoisFatores> {
    return this.http.get<StatusDoisFatores>(`${this.baseUrl}/2fa`);
  }

  iniciarDoisFatores(): Observable<ConfiguracaoDoisFatores> {
    return this.http.post<ConfiguracaoDoisFatores>(`${this.baseUrl}/2fa/iniciar`, {});
  }

  /** Confirma o primeiro código do aplicativo e devolve os códigos de recuperação (aparecem só desta vez). */
  ativarDoisFatores(codigo: string): Observable<{ codigos: string[] }> {
    return this.http.post<{ codigos: string[] }>(`${this.baseUrl}/2fa/ativar`, { codigo });
  }

  desativarDoisFatores(senha: string, codigo: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/2fa/desativar`, { senha, codigo });
  }

  encerrarConta(senha: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/encerrar-conta`, { senha }, { withCredentials: true })
      .pipe(map(() => this.limparStorage()));
  }

  /** ROTACIONAR TOKEN */
  rotacionarToken(): Observable<AccessToken> {
    const token = this.getAccessToken();
    return this.http.post<RespostaToken>(`${this.baseUrl}/rotacionar`, {}, {
      headers: this.cabecalhos(token),
      withCredentials: true,
      // Quem chama (restaurar sessão ao abrir o site, renovar no interceptor) trata a falha: sem toast
      // de "Não autorizado" para quem simplesmente ainda não fez login.
      context: new HttpContext().set(SILENCIAR_ERRO, true)
    }).pipe(
      map(response => this.mapTokenResponse(response)),
      catchError(err => throwError(() => err))
    );
  }

  /** LOGOUT */
  sair(): Observable<void> {
    const token = this.getAccessToken();
    return this.http.post<void>(`${this.baseUrl}/sair`, {}, {
      headers: this.cabecalhos(token),
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

  private restauracaoEmAndamento$: Observable<boolean> | null = null;

  /**
   * Garante que há sessão em memória, restaurando-a pelo refresh token se preciso. Chamadas
   * simultâneas COMPARTILHAM a mesma requisição: o Angular roda os guards de uma mesma rota em
   * paralelo (AuthGuard e roleGuard) e o refresh token é rotativo -- duas chamadas a
   * /auth/rotacionar com o mesmo cookie fariam a segunda falhar e derrubar a sessão.
   */
  garantirSessao(): Observable<boolean> {
    if (this.isLoggedIn()) return of(true);

    if (!this.restauracaoEmAndamento$) {
      this.restauracaoEmAndamento$ = this.tentarRestaurarSessao().pipe(
        finalize(() => (this.restauracaoEmAndamento$ = null)),
        shareReplay(1)
      );
    }
    return this.restauracaoEmAndamento$;
  }

  /** MAPEAR RESPOSTA DO BACKEND PARA AccessToken */
  private mapTokenResponse(response: RespostaToken): AccessToken {
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
