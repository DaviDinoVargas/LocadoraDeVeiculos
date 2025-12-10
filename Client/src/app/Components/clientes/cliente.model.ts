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
  cpf: string; // Formato: 000.000.000-00
  rg?: string;
  cnh?: string;
  validadeCnh?: string;
  telefone: string; // Formato: (00) 00000-0000
  email?: string;
  endereco?: string;
  clientePessoaJuridicaId?: string | null;
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
