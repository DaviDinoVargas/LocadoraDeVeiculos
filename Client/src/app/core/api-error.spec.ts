import { HttpErrorResponse } from '@angular/common/http';
import { mensagensDeErro } from './api-error';

describe('mensagensDeErro', () => {
  const http = (error: unknown, status = 400) => new HttpErrorResponse({ status, error });

  it('lê o array de strings que a Web API devolve nos erros de negócio', () => {
    expect(mensagensDeErro(http(['O automóvel já está reservado.', 'Outra']))).toEqual(['O automóvel já está reservado.', 'Outra']);
  });

  it('lê corpo em texto puro', () => {
    expect(mensagensDeErro(http('Falhou'))).toEqual(['Falhou']);
  });

  it('lê o formato de validação do ASP.NET (errors)', () => {
    expect(mensagensDeErro(http({ errors: { Nome: ['Obrigatório'], Cpf: ['Inválido'] } }))).toEqual(['Obrigatório', 'Inválido']);
  });

  it('lê message/detail/title', () => {
    expect(mensagensDeErro(http({ message: 'Oi' }))).toEqual(['Oi']);
    expect(mensagensDeErro(http({ title: 'Título' }))).toEqual(['Título']);
  });

  it('ignora itens que não são texto e cai no padrão quando não há nada útil', () => {
    expect(mensagensDeErro(http([1, null, ' ']), 'padrão')).toEqual(['padrão']);
    expect(mensagensDeErro(http(null), 'padrão')).toEqual(['padrão']);
    expect(mensagensDeErro(undefined)).toEqual(['Erro desconhecido']);
  });
});
