// Base da Web API vista pelo navegador. É um caminho RELATIVO de propósito: o front e a
// API passam a ser a mesma origem para o navegador, com um proxy no meio
//   - em desenvolvimento: proxy.conf.json (ng serve) -> https://localhost:7064
//   - em Docker/produção:  nginx (location /api/)   -> web-api
// Assim o cookie de refresh token (HttpOnly; Secure; SameSite=Lax) é enviado em
// /api/auth/rotacionar, o que NÃO acontecia quando o front (http://localhost:4200) falava
// direto com https://localhost:7064 (para o Chrome, esquemas diferentes são "cross-site").
export const API_BASE_URL = '/api';
