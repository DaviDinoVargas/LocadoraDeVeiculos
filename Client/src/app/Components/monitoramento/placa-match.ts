/**
 * Casa a placa lida pelo OCR da câmera com as placas dos aluguéis em aberto.
 *
 * O OCR erra caracteres parecidos (R lido como K, B como 8, O como 0 ...), principalmente com a
 * placa pequena ou desfocada. Como as placas cadastradas são conhecidas, a leitura vale se for
 * igual a uma delas ou se diferir em UM caractere que seja uma confusão típica de OCR — e só quando
 * essa é a única candidata (senão é ambíguo e não adivinhamos).
 */

const PARES_CONFUNDIVEIS: ReadonlyArray<readonly [string, string]> = [
  ['R', 'K'], ['R', 'B'], ['R', 'P'], ['B', '8'], ['B', 'E'], ['K', 'X'],
  ['O', '0'], ['O', 'Q'], ['O', 'D'], ['0', 'D'], ['0', 'Q'], ['D', 'Q'],
  ['I', '1'], ['I', 'L'], ['1', 'L'], ['I', 'T'], ['T', '7'],
  ['S', '5'], ['Z', '2'], ['G', '6'], ['C', 'G'], ['A', '4'],
  ['U', 'V'], ['M', 'N'], ['H', 'N'], ['E', 'F']
];

const CONFUNDIVEIS = new Set(PARES_CONFUNDIVEIS.flatMap(([a, b]) => [a + b, b + a]));

/** Só letras e dígitos, em maiúsculas ("BRA-2E19" → "BRA2E19"). */
export function normalizarPlaca(placa: string | null | undefined): string {
  return (placa ?? '').toUpperCase().replace(/[^A-Z0-9]/g, '');
}

/** Quantas posições diferem, ou `null` se o tamanho for outro ou alguma diferença não for uma confusão típica de OCR. */
function distanciaConfundivel(lida: string, cadastrada: string): number | null {
  if (lida.length !== cadastrada.length) return null;
  let diferencas = 0;
  for (let i = 0; i < lida.length; i++) {
    if (lida[i] === cadastrada[i]) continue;
    if (!CONFUNDIVEIS.has(lida[i] + cadastrada[i])) return null;
    diferencas++;
  }
  return diferencas;
}

/**
 * Devolve a placa cadastrada que corresponde à lida: a igual, ou a única que difere em no máximo
 * `maxDiferencas` caracteres confundíveis. `null` se não houver ou se houver mais de uma candidata.
 */
export function casarPlaca(lida: string, cadastradas: Iterable<string>, maxDiferencas = 1): string | null {
  const alvo = normalizarPlaca(lida);
  if (!alvo) return null;

  const lista = Array.from(new Set(Array.from(cadastradas, normalizarPlaca).filter(Boolean)));
  if (lista.includes(alvo)) return alvo;

  const candidatas = lista.filter(c => {
    const d = distanciaConfundivel(alvo, c);
    return d !== null && d <= maxDiferencas;
  });
  return candidatas.length === 1 ? candidatas[0] : null;
}
