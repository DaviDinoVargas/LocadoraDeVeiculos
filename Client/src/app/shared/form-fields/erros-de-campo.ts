import { AbstractControl, FormGroup } from '@angular/forms';

/** minúsculas, sem acentos e com "e-mail" escrito como "email", para comparar nome de campo e texto do servidor */
function normalizar(texto: string): string {
  return texto
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/e-mail/g, 'email');
}

/** `automovelId` -> ['automovel'], `dataSaida` -> ['data', 'saida'] (palavras curtas e "id" não identificam campo) */
function palavrasDoCampo(nome: string): string[] {
  return nome
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .split(' ')
    .map(normalizar)
    .filter(p => p.length >= 3 && p !== 'id');
}

function contemPalavra(texto: string, palavra: string): boolean {
  return new RegExp(`(^|[^a-z0-9])${palavra}([^a-z0-9]|$)`).test(texto);
}

/**
 * A Web API devolve os erros de negócio como uma lista de textos, sem dizer a qual campo se referem
 * ("Já existe um automóvel com esta placa."). Esta função liga cada texto ao campo cujo nome aparece nele
 * (`placa`, `cpf`, `cnh`, `nome`...) e grava o texto como erro `servidor` do controle: ele aparece embaixo do
 * campo, o 1º campo com erro recebe o foco e o erro some quando o usuário edita o valor (o Angular reexecuta os
 * validadores a cada alteração). Só atribui quando o texto aponta para **exatamente um** campo; os demais
 * continuam no alerta geral do topo. `apelidos` acrescenta palavras por campo (ex.: `{ documento: ['cpf', 'cnpj'] }`).
 * Devolve os textos que ficaram sem campo.
 */
export function distribuirErrosNosCampos(
  form: FormGroup,
  mensagens: string[],
  apelidos: Record<string, string[]> = {}
): string[] {
  const campos = Object.entries(form.controls).map(([nome, controle]) => ({
    controle: controle as AbstractControl,
    palavras: [...palavrasDoCampo(nome), ...(apelidos[nome] ?? []).map(normalizar)]
  }));

  const restantes: string[] = [];
  const usados = new Set<AbstractControl>();

  for (const mensagem of mensagens) {
    const texto = normalizar(mensagem);
    const candidatos = campos.filter(c => c.palavras.some(p => contemPalavra(texto, p)));
    const alvo = candidatos.length === 1 ? candidatos[0].controle : null;

    if (alvo && !usados.has(alvo)) {
      usados.add(alvo);
      alvo.setErrors({ ...(alvo.errors ?? {}), servidor: mensagem });
      alvo.markAsTouched();
    } else {
      restantes.push(mensagem);
    }
  }

  return restantes;
}
