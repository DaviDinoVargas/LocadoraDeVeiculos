// Fonte única dos papéis (roles) usados pelo front: rotas (roleGuard), sidebar e Home leem
// daqui. Os valores espelham o que a Web API realmente aplica (verificado endpoint a
// endpoint com um usuário Funcionario):
//
//   somente Empresa .......... funcionarios, grupos-automovel, planos-cobranca, parceiros,
//                              configuracoes  (o [Authorize] da classe e o do método se
//                              SOMAM no ASP.NET, por isso GET /parceiros também é só Empresa)
//   Empresa + Funcionario .... automoveis (leitura), clientes, condutores, alugueis,
//                              devolucoes, taxas-servicos (leitura)
//   leitura p/ ambos, escrita
//   só Empresa ............... automoveis, cupons, desafios-cupom, taxas-servicos (S32: preço é decisão da gerência)
//
// As telas de Cupons/Desafios são de GESTÃO (CRUD) e por isso ficam só para Empresa no
// front, embora a API deixe o Funcionario ler (o formulário de aluguel usa cupons).
export type Cargo = 'Empresa' | 'Funcionario';

export const SOMENTE_EMPRESA: readonly Cargo[] = ['Empresa'];
export const EMPRESA_E_FUNCIONARIO: readonly Cargo[] = ['Empresa', 'Funcionario'];

export function cargoPodeAcessar(
  cargo: string | null | undefined,
  papeisPermitidos: readonly string[] | undefined
): boolean {
  if (!papeisPermitidos) return true;
  return !!cargo && papeisPermitidos.includes(cargo);
}
