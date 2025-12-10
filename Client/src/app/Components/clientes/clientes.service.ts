import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { ClienteDto, ClientePessoaFisicaDto, ClientePessoaJuridicaDto } from './cliente.model';

@Injectable({ providedIn: 'root' })
export class ClientesService {
  private base = 'https://localhost:7064/api/clientes';

  constructor(private http: HttpClient) {}

  private extractData<T>(raw: any): T {
    if (!raw) return raw;
    if (raw.dados) return raw.dados as T;
    if (raw.registros) return raw.registros as T;
    return raw as T;
  }

  listar(): Observable<ClienteDto[]> {
    return this.http.get<any>(this.base).pipe(map(raw => this.extractData<ClienteDto[]>(raw)));
  }

  listarPessoasFisicas(): Observable<ClientePessoaFisicaDto[]> {
    return this.http.get<any>(`${this.base}/pessoas-fisicas`).pipe(map(raw => this.extractData<ClientePessoaFisicaDto[]>(raw)));
  }

  listarPessoasJuridicas(): Observable<ClientePessoaJuridicaDto[]> {
    return this.http.get<any>(`${this.base}/pessoas-juridicas`).pipe(map(raw => this.extractData<ClientePessoaJuridicaDto[]>(raw)));
  }

  obter(id: string): Observable<ClienteDto> {
    return this.http.get<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<ClienteDto>(raw)));
  }

  criarPessoaFisica(payload: ClientePessoaFisicaDto): Observable<ClientePessoaFisicaDto> {
    return this.http.post<any>(`${this.base}/pessoa-fisica`, payload).pipe(map(raw => this.extractData<ClientePessoaFisicaDto>(raw)));
  }

  criarPessoaJuridica(payload: ClientePessoaJuridicaDto): Observable<ClientePessoaJuridicaDto> {
    return this.http.post<any>(`${this.base}/pessoa-juridica`, payload).pipe(map(raw => this.extractData<ClientePessoaJuridicaDto>(raw)));
  }

  atualizarPessoaFisica(id: string, payload: ClientePessoaFisicaDto): Observable<ClientePessoaFisicaDto> {
    return this.http.put<any>(`${this.base}/pessoa-fisica/${id}`, payload).pipe(map(raw => this.extractData<ClientePessoaFisicaDto>(raw)));
  }

  atualizarPessoaJuridica(id: string, payload: ClientePessoaJuridicaDto): Observable<ClientePessoaJuridicaDto> {
    return this.http.put<any>(`${this.base}/pessoa-juridica/${id}`, payload).pipe(map(raw => this.extractData<ClientePessoaJuridicaDto>(raw)));
  }

  excluir(id: string): Observable<void> {
    return this.http.delete<any>(`${this.base}/${id}`).pipe(map(raw => this.extractData<void>(raw)));
  }
}
