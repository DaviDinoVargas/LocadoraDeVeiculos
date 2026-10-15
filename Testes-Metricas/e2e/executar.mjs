// Roda o Playwright (fluxos + acessibilidade) e resume os resultados e os tempos das tarefas em e2e/resumo.md.
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const aqui = dirname(fileURLToPath(import.meta.url));
const saida = join(process.env.METRICAS_SAIDA ?? '.', 'e2e');
mkdirSync(saida, { recursive: true });

const bin = join(aqui, '..', 'node_modules', '.bin', process.platform === 'win32' ? 'playwright.cmd' : 'playwright');
const extras = (process.env.METRICAS_E2E_ARGS ?? '').split(' ').filter(Boolean);   // ex.: --grep "cliente"
const r = spawnSync(bin, ['test', '--config', join(aqui, 'playwright.config.mjs'), ...extras], {
  stdio: 'inherit', env: process.env, shell: process.platform === 'win32'
});

const linhas = f => (existsSync(f) ? readFileSync(f, 'utf-8').trim().split('\n').filter(Boolean).map(l => JSON.parse(l)) : []);
const tarefas = linhas(join(saida, 'tarefas.jsonl'));
const acess = linhas(join(saida, 'acessibilidade.jsonl'));
let testes = [];
try {
  const j = JSON.parse(readFileSync(join(saida, 'playwright.json'), 'utf-8'));
  const colher = s => [...(s.specs ?? []).map(sp => ({ titulo: sp.title, ok: sp.ok })), ...(s.suites ?? []).flatMap(colher)];
  testes = (j.suites ?? []).flatMap(colher);
} catch { /* sem json: o Playwright falhou antes de rodar */ }

const md = [`# Ponta a ponta (Playwright, Edge) — ${new Date().toISOString().slice(0, 19)}`, '',
  `- Commit \`${process.env.METRICAS_COMMIT ?? 'sem-commit'}\` · build de produção · ${testes.filter(t => t.ok).length}/${testes.length} testes aprovados`, '',
  '## Testes', '', '| Teste | Resultado |', '|---|---|', ...testes.map(t => `| ${t.titulo} | ${t.ok ? 'OK' : '**FALHA**'} |`), '',
  '## Tarefas cronometradas (linha de base automatizada, sem hesitação humana)', '',
  '| Tarefa | Tempo (s) | Cliques | Preenchimentos | Erro |', '|---|---|---|---|---|',
  ...tarefas.map(t => `| ${t.tarefa} | ${(t.tempo_ms / 1000).toFixed(1)} | ${t.cliques} | ${t.preenchimentos} | ${t.erro ?? '—'} |`), '',
  '## axe-core na página inteira', '', '| Página | Violações |', '|---|---|',
  ...acess.map(a => `| ${a.pagina} | ${a.violacoes}${a.violacoes ? ` (${a.regras.join(', ')})` : ''} |`), ''];
writeFileSync(join(saida, 'resumo.md'), md.join('\n'));
console.log(`\nResumo: ${join(saida, 'resumo.md')}`);
process.exit(r.status ?? 1);
