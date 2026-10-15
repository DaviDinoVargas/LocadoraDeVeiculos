// Apoio dos fluxos: login pela tela, geradores de dados válidos, cronômetro de tarefa e auditoria de acessibilidade.
import { appendFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import AxeBuilder from '@axe-core/playwright';
import { expect } from '@playwright/test';

export const EMAIL = process.env.METRICAS_EMAIL;
export const SENHA = process.env.METRICAS_SENHA;
export const ALUGUEIS = JSON.parse(process.env.METRICAS_ALUGUEIS ?? '{}');
const SAIDA = join(process.env.METRICAS_SAIDA ?? '.', 'e2e');

/** Controle de um campo do formulário pelo texto do rótulo (o rótulo do app traz "(obrigatório)" junto, e os selects são nativos dentro do wrapper). */
export const campo = (page, rotulo) => page.locator('app-form-field')
  .filter({ has: page.locator('label').filter({ hasText: new RegExp(String.raw`^\s*${rotulo}(\s|$)`) }) })
  .locator('input, textarea, select').first();

/** Dá ao navegador do teste um IP simulado novo (ver servidor_front.py): o limite de autenticação da API é por IP e cada recarga o consome. */
export async function novoIp(page) {
  const ip = `10.${1 + Math.floor(Math.random() * 250)}.${1 + Math.floor(Math.random() * 250)}.${1 + Math.floor(Math.random() * 250)}`;
  await page.context().setExtraHTTPHeaders({ 'X-Metricas-Ip': ip });
}

export async function entrar(page) {
  await novoIp(page);
  await page.goto('/login');
  await page.getByLabel('E-mail').fill(EMAIL);
  await page.locator('input[type="password"]').fill(SENHA);
  const [resposta] = await Promise.all([
    page.waitForResponse(r => r.url().includes('/api/auth/autenticar')),
    page.getByRole('button', { name: 'Entrar', exact: true }).click()
  ]);
  if (!resposta.ok()) {
    throw new Error(`login recusado pela API: HTTP ${resposta.status()} (${(await resposta.text()).slice(0, 120)}) ${JSON.stringify(await resposta.allHeaders()).match(/retry-after[^,]*/i)?.[0] ?? ''}`);
  }
  await expect(page).not.toHaveURL(/\/login/);
}

/** CPF com dígitos verificadores corretos (o formulário valida no front). */
export function cpfValido() {
  const d = Array.from({ length: 9 }, () => Math.floor(Math.random() * 10));
  if (new Set(d).size === 1) d[0] = (d[0] + 1) % 10;
  for (const tamanho of [9, 10]) {
    const soma = d.reduce((s, n, i) => s + n * (tamanho + 1 - i), 0);
    const resto = soma % 11;
    d.push(resto < 2 ? 0 : 11 - resto);
  }
  const s = d.join('');
  return `${s.slice(0, 3)}.${s.slice(3, 6)}.${s.slice(6, 9)}-${s.slice(9)}`;
}

export const dataIso = (diasAPartirDeHoje) => {
  const d = new Date(Date.now() + diasAPartirDeHoje * 86_400_000);
  return d.toISOString().slice(0, 10);
};

/**
 * Cronometra uma tarefa de usuário (do primeiro clique até o resultado visível) e registra o tempo, o número de
 * ações e se houve erro. É a linha de base AUTOMATIZADA (sem hesitação humana) para comparar com as tarefas feitas por pessoas.
 */
export async function tarefa(nome, corpo) {
  mkdirSync(SAIDA, { recursive: true });
  const acoes = { cliques: 0, preenchimentos: 0 };
  const inicio = performance.now();
  let erro = null;
  try {
    await corpo(acoes);
  } catch (e) {
    erro = String(e?.message ?? e).split('\n')[0];
    throw e;
  } finally {
    const registro = { tarefa: nome, tempo_ms: Math.round(performance.now() - inicio), ...acoes, erro };
    appendFileSync(join(SAIDA, 'tarefas.jsonl'), JSON.stringify(registro) + '\n');
  }
}

/** Clica e conta como uma ação da tarefa. */
export async function clicar(acoes, localizador) { acoes.cliques++; await localizador.click(); }
/** Preenche e conta como uma ação da tarefa. */
export async function preencher(acoes, localizador, valor) { acoes.preenchimentos++; await localizador.fill(valor); }

/** axe-core na página inteira (inclui landmarks, que o auditor de componentes não vê). Falha com a lista de violações. */
export async function semViolacoes(page, rotulo) {
  const r = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa', 'best-practice']).analyze();
  const linhas = r.violations.map(v => `${v.id} (${v.impact}): ${v.help} — ${v.nodes.length} elemento(s): ${v.nodes.slice(0, 3).map(n => n.target.join(' ')).join(' | ')}`);
  appendFileSync(join(SAIDA, 'acessibilidade.jsonl'), JSON.stringify({ pagina: rotulo, violacoes: r.violations.length, regras: r.violations.map(v => v.id) }) + '\n');
  expect(linhas, `acessibilidade em ${rotulo}`).toEqual([]);
}
