import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface FaceEnrollResponse {
  ok: boolean;
  personId: string;
  amostras: number;
}

export interface FaceVerifyResponse {
  match: boolean;
  confidence: number;
}

export interface FaceStatusResponse {
  personId: string;
  cadastrado: boolean;
}

@Injectable({ providedIn: 'root' })
export class FaceAuthService {
  private baseUrl = '/api/ml/face';

  constructor(private http: HttpClient) {}

  status(personId: string): Observable<FaceStatusResponse> {
    return this.http.get<FaceStatusResponse>(`${this.baseUrl}/status/${encodeURIComponent(personId)}`);
  }

  cadastrar(personId: string, imageBase64: string): Observable<FaceEnrollResponse> {
    return this.http.post<FaceEnrollResponse>(`${this.baseUrl}/enroll`, { personId, imageBase64 });
  }

  verificar(personId: string, imageBase64: string): Observable<FaceVerifyResponse> {
    return this.http.post<FaceVerifyResponse>(`${this.baseUrl}/verify`, { personId, imageBase64 });
  }

  remover(personId: string): Observable<{ ok: boolean }> {
    return this.http.delete<{ ok: boolean }>(`${this.baseUrl}/${encodeURIComponent(personId)}`);
  }
}
