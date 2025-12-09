// auth.models.ts
export interface AccessToken {
  accessToken: string;
  expires: string; // ISO string
  usuario?: Usuario; 
}

export interface Usuario {
  id: string;
  nomeCompleto: string;
  email: string;
}

