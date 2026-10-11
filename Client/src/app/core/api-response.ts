// A Web API devolve listas como { "registros": [...] } (resposta plana) e objetos diretamente.
// Substitui o `extractData(raw: any)` que cada service repetia (inclusive um ramo `dados` morto, de
// um wrapper { sucesso, dados } que foi removido do backend).
export function extrairDados<T>(resposta: unknown): T {
  if (resposta && typeof resposta === 'object' && 'registros' in resposta) {
    return (resposta as { registros: unknown }).registros as T;
  }
  return resposta as T;
}
