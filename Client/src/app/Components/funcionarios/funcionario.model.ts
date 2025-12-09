export interface FuncionarioDto {
  id?: string;
  nomeCompleto: string;
  cpf: string;
  email?: string;
  senha?: string;
  confirmarSenha?: string; 
  salario: number;
  admissaoEmUtc: string;
}
