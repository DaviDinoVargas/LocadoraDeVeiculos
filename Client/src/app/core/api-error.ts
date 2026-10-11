// A Web API devolve os erros de negócio como um ARRAY de strings no corpo
// (MainController.MapearErro: BadRequest/Conflict/NotFound/UnprocessableEntity(detalhes)).
// O interceptor e os formulários ignoravam esse formato: o usuário via "Dados inválidos" ou o
// texto genérico "Http failure response...", e mensagens como "O automóvel já está reservado
// para este período." nunca chegavam à tela.
export function mensagensDeErro(err: unknown, padrao = 'Erro desconhecido'): string[] {
  const corpo = (err as { error?: unknown } | null)?.error;
  const mensagens: string[] = [];

  if (Array.isArray(corpo)) {
    mensagens.push(...corpo.filter((m): m is string => typeof m === 'string' && m.trim().length > 0));
  } else if (typeof corpo === 'string' && corpo.trim()) {
    mensagens.push(corpo);
  } else if (corpo && typeof corpo === 'object') {
    const obj = corpo as { errors?: Record<string, string[]>; message?: string; detail?: string; title?: string };
    if (obj.errors && typeof obj.errors === 'object') {
      mensagens.push(...Object.values(obj.errors).flat());
    }
    const texto = obj.message ?? obj.detail ?? obj.title;
    if (!mensagens.length && typeof texto === 'string' && texto.trim()) {
      mensagens.push(texto);
    }
  }

  return mensagens.length ? mensagens : [padrao];
}
