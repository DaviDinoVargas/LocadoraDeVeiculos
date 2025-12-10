export interface ClienteDto {
  id?: string;
  nome: string;
  telefone: string;
  email?: string;
  endereco?: string;
  tipoCliente: 'PessoaFisica' | 'PessoaJuridica';
  cpf?: string;
  rg?: string;
  cnh?: string;
  validadeCnh?: string;
  clientePessoaJuridicaId?: string;
  clientePessoaJuridicaNome?: string;
  cnpj?: string;
  nomeFantasia?: string;
}

export interface ClientePessoaFisicaDto {
  id?: string;
  nome: string;
  cpf: string;
  rg?: string;
  cnh?: string;
  validadeCnh?: string;
  telefone: string;
  email?: string;
  endereco?: string;
  clientePessoaJuridicaId?: string;
  clientePessoaJuridicaNome?: string;
}

export interface ClientePessoaJuridicaDto {
  id?: string;
  nome: string;
  cnpj: string;
  nomeFantasia?: string;
  telefone: string;
  email?: string;
  endereco?: string;
}
