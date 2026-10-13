/** Registro da trilha de auditoria: guarda só nomes e identificadores, nunca os valores. */
export interface RegistroAuditoria {
  id: string;
  emUtc: string;
  usuarioId: string | null;
  acao: string;
  entidade: string;
  entidadeId: string | null;
  camposAlterados: string | null;
  enderecoIp: string | null;
}
