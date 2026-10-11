import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { ClienteDto, ClientePessoaFisicaDto, ClientePessoaJuridicaDto } from './cliente.model';
import { API_BASE_URL } from '../../core/api.config';
import { extrairDados } from '../../core/api-response';

@Injectable({ providedIn: 'root' })
export class ClientesService {
  private base = `${API_BASE_URL}/clientes`;

  constructor(private http: HttpClient) {}


  listar(): Observable<ClienteDto[]> {
    return this.http.get<unknown>(this.base).pipe(map(raw => extrairDados<ClienteDto[]>(raw)));
  }

  listarPessoasFisicas(): Observable<ClientePessoaFisicaDto[]> {
    return this.http.get<unknown>(`${this.base}/pessoas-fisicas`).pipe(map(raw => extrairDados<ClientePessoaFisicaDto[]>(raw)));
  }

  listarPessoasJuridicas(): Observable<ClientePessoaJuridicaDto[]> {
    return this.http.get<unknown>(`${this.base}/pessoas-juridicas`).pipe(map(raw => extrairDados<ClientePessoaJuridicaDto[]>(raw)));
  }

  obter(id: string): Observable<ClienteDto> {
    return this.http.get<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<ClienteDto>(raw)));
  }

  criarPessoaFisica(payload: ClientePessoaFisicaDto): Observable<ClientePessoaFisicaDto> {
    return this.http.post<unknown>(`${this.base}/pessoa-fisica`, payload).pipe(map(raw => extrairDados<ClientePessoaFisicaDto>(raw)));
  }

  criarPessoaJuridica(payload: ClientePessoaJuridicaDto): Observable<ClientePessoaJuridicaDto> {
    return this.http.post<unknown>(`${this.base}/pessoa-juridica`, payload).pipe(map(raw => extrairDados<ClientePessoaJuridicaDto>(raw)));
  }

  atualizarPessoaFisica(id: string, payload: ClientePessoaFisicaDto): Observable<ClientePessoaFisicaDto> {
    return this.http.put<unknown>(`${this.base}/pessoa-fisica/${id}`, payload).pipe(map(raw => extrairDados<ClientePessoaFisicaDto>(raw)));
  }

  atualizarPessoaJuridica(id: string, payload: ClientePessoaJuridicaDto): Observable<ClientePessoaJuridicaDto> {
    return this.http.put<unknown>(`${this.base}/pessoa-juridica/${id}`, payload).pipe(map(raw => extrairDados<ClientePessoaJuridicaDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<unknown>(`${this.base}/${id}`).pipe(map(raw => extrairDados<void>(raw)));
  }
}
