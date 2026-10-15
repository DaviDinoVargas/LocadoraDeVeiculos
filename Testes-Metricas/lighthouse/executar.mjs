// Lighthouse nas telas-chave, em desktop e celular, contra o build de PRODUÇÃO (ver orquestrar.py).
// Variáveis (passadas pelo orquestrador; credenciais nunca são impressas): METRICAS_URL, METRICAS_EMAIL, METRICAS_SENHA,
// METRICAS_SAIDA, METRICAS_ALUGUEIS (json), METRICAS_RODADAS (padrão 3), EDGE_PATH (padrão: Edge instalado).
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import lighthouse from 'lighthouse';
import desktopConfig from 'lighthouse/core/config/desktop-config.js';
import puppeteer from 'puppeteer-core';

const URL_BASE = process.env.METRICAS_URL;
const EMAIL = process.env.METRICAS_EMAIL;
const SENHA = process.env.METRICAS_SENHA;
const SAIDA = join(process.env.METRICAS_SAIDA ?? '.', 'lighthouse');
const RODADAS = Number(process.env.METRICAS_RODADAS ?? 3);
const ALUGUEIS = JSON.parse(process.env.METRICAS_ALUGUEIS ?? '{}');
const PORTA = 9333;
const EDGE = process.env.EDGE_PATH
  ?? ['C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe', 'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe'].find(existsSync);

if (!URL_BASE || !EMAIL || !SENHA) throw new Error('Execute pelo orquestrar.py (faltam METRICAS_URL/EMAIL/SENHA).');
if (!EDGE) throw new Error('Edge não encontrado; defina EDGE_PATH.');

const emAndamento = ALUGUEIS.em_andamento?.[0];

/** Telas-chave: a de login é medida SEM sessão; as demais, logadas. */
const FILTRO = (process.env.METRICAS_TELAS ?? '').split(',').map(s => s.trim()).filter(Boolean);   // ex.: home,alugueis
const TODAS_AS_TELAS = [
  { id: 'login', caminho: '/login', logada: false },
  { id: 'home', caminho: '/home' },
  { id: 'clientes', caminho: '/clientes' },
  { id: 'condutores', caminho: '/condutores' },
  { id: 'veiculos', caminho: '/veiculos' },
  { id: 'alugueis', caminho: '/alugueis' },
  { id: 'devolucoes', caminho: '/devolucoes' },
  { id: 'aluguel-form', caminho: '/alugueis/new' },
  { id: 'veiculo-form', caminho: '/veiculos/new' },
  { id: 'cliente-form', caminho: '/clientes/new/pf' },
  ...(emAndamento ? [{ id: 'devolucao-form', caminho: `/alugueis/${emAndamento}/devolver` }] : [])
];
const FORMATOS = ['desktop', 'mobile'];
const TELAS = FILTRO.length ? [TODAS_AS_TELAS[0], ...TODAS_AS_TELAS.slice(1).filter(t => FILTRO.includes(t.id))] : TODAS_AS_TELAS;

const dormir = ms => new Promise(r => setTimeout(r, ms));
const mediana = v => { const o = [...v].sort((a, b) => a - b); const m = Math.floor(o.length / 2); return o.length % 2 ? o[m] : (o[m - 1] + o[m]) / 2; };

async function iniciarEdge() {
  const perfil = mkdtempSync(join(tmpdir(), 'metricas-edge-'));
  const proc = spawn(EDGE, [`--remote-debugging-port=${PORTA}`, `--user-data-dir=${perfil}`, '--headless=new', '--no-first-run',
    '--no-default-browser-check', '--disable-gpu', '--disable-extensions', '--disable-background-networking', 'about:blank'], { stdio: 'ignore' });
  for (let i = 0; i < 60; i++) {
    try { const r = await fetch(`http://127.0.0.1:${PORTA}/json/version`); if (r.ok) return { proc, perfil }; } catch { /* ainda subindo */ }
    await dormir(500);
  }
  proc.kill();
  throw new Error('Edge não abriu a porta de depuração.');
}

async function entrar(browser) {
  const pagina = await browser.newPage();
  await pagina.goto(`${URL_BASE}/login`, { waitUntil: 'networkidle0' });
  await pagina.type('input[type="email"]', EMAIL);
  await pagina.type('input[type="password"]', SENHA);
  await Promise.all([pagina.waitForFunction(() => !location.pathname.startsWith('/login'), { timeout: 30000 }), pagina.click('button[type="submit"]')]);
  await pagina.close();
}

const num = (lhr, id) => lhr.audits[id]?.numericValue ?? null;

/** Auditorias de acessibilidade/boas práticas com nota < 1 e as maiores oportunidades de desempenho (para saber o que corrigir). */
function achados(lhr) {
  const falhas = [];
  for (const cat of ['accessibility', 'best-practices']) {
    for (const ref of lhr.categories[cat].auditRefs) {
      const a = lhr.audits[ref.id];
      if (ref.weight > 0 && a.score !== null && a.score < 1) falhas.push({ categoria: cat, id: a.id, titulo: a.title, nota: a.score });
    }
  }
  const oportunidades = Object.values(lhr.audits)
    .filter(a => a.details?.type === 'opportunity' && (a.details.overallSavingsMs ?? 0) >= 150)
    .map(a => ({ id: a.id, titulo: a.title, economia_ms: Math.round(a.details.overallSavingsMs) }))
    .sort((x, y) => y.economia_ms - x.economia_ms).slice(0, 5);
  const deslocamentos = (lhr.audits['layout-shifts']?.details?.items ?? []).slice(0, 3)
    .map(i => ({ pontuacao: Number((i.score ?? 0).toFixed(3)), elemento: i.node?.selector ?? i.node?.snippet ?? '' }));
  return { falhas, oportunidades, deslocamentos };
}

function resumir(lhr) {
  return {
    ...achados(lhr),
    desempenho: Math.round((lhr.categories.performance.score ?? 0) * 100),
    acessibilidade: Math.round((lhr.categories.accessibility.score ?? 0) * 100),
    boasPraticas: Math.round((lhr.categories['best-practices'].score ?? 0) * 100),
    fcp_ms: Math.round(num(lhr, 'first-contentful-paint')),
    lcp_ms: Math.round(num(lhr, 'largest-contentful-paint')),
    cls: Number((num(lhr, 'cumulative-layout-shift') ?? 0).toFixed(3)),
    tbt_ms: Math.round(num(lhr, 'total-blocking-time')),
    si_ms: Math.round(num(lhr, 'speed-index')),
    transferencia_kb: Math.round((lhr.audits['total-byte-weight']?.numericValue ?? 0) / 1024),
    urlFinal: lhr.finalDisplayedUrl
  };
}

async function medir(browser, tela, formato) {
  const url = `${URL_BASE}${tela.caminho}`;
  const rodadas = [];
  let melhorHtml = null;
  for (let i = 0; i < RODADAS; i++) {
    for (let tentativa = 0; tentativa < 2; tentativa++) {
      // cache frio a cada rodada (a sessão fica: o cookie de renovação não é "cache")
      const p = await browser.newPage();
      const cdp = await p.createCDPSession();
      await cdp.send('Network.clearBrowserCache');
      await p.close();

      const opcoes = { port: PORTA, output: ['json', 'html'], logLevel: 'error', disableStorageReset: true,
        onlyCategories: ['performance', 'accessibility', 'best-practices'] };
      const r = await lighthouse(url, opcoes, formato === 'desktop' ? desktopConfig : undefined);
      const [json, html] = r.report;
      const lhr = JSON.parse(json);
      const resumo = resumir(lhr);
      const caiuNoLogin = tela.logada !== false && new URL(resumo.urlFinal).pathname.startsWith('/login');
      if (caiuNoLogin) { await entrar(browser); continue; }   // a sessão expirou: entra de novo e repete
      rodadas.push({ ...resumo, _html: html });
      break;
    }
  }
  if (!rodadas.length) throw new Error(`${tela.id}/${formato}: sem rodada válida (a tela redirecionou para o login)`);
  const ordenadas = [...rodadas].sort((a, b) => a.lcp_ms - b.lcp_ms);
  melhorHtml = ordenadas[Math.floor(ordenadas.length / 2)]._html;      // relatório da rodada mediana (por LCP)
  const chaves = ['desempenho', 'acessibilidade', 'boasPraticas', 'fcp_ms', 'lcp_ms', 'cls', 'tbt_ms', 'si_ms', 'transferencia_kb'];
  const mediana_ = Object.fromEntries(chaves.map(c => [c, Number(mediana(rodadas.map(r => r[c])).toFixed(3))]));
  mkdirSync(join(SAIDA, 'relatorios'), { recursive: true });
  writeFileSync(join(SAIDA, 'relatorios', `${tela.id}-${formato}.html`), melhorHtml);
  const doMeio = ordenadas[Math.floor(ordenadas.length / 2)];
  return { tela: tela.id, caminho: tela.caminho, formato, rodadas: rodadas.length, mediana: mediana_,
    falhas: doMeio.falhas, oportunidades: doMeio.oportunidades, deslocamentos: doMeio.deslocamentos,
    todas: rodadas.map(({ _html, falhas, oportunidades, deslocamentos, ...resto }) => resto) };
}

function tabelaMd(resultados, meta) {
  const l = [`# Lighthouse — ${meta.data}`, '',
    `- Commit \`${meta.commit}\` · Edge ${meta.edge} · Lighthouse ${meta.lighthouse} · ${RODADAS} rodada(s) por tela, **mediana** · build de produção, cache frio`,
    '- Lighthouse mede em laboratório: INP não existe nesse modo; **TBT (tempo de bloqueio)** é o substituto usual. Telas logadas usam a sessão da empresa de teste.',
    '- Desempenho usa a simulação de rede/CPU padrão do Lighthouse (celular: 4G lenta, CPU 4x mais lenta; desktop: sem estrangulamento forte).', ''];
  for (const formato of FORMATOS) {
    l.push(`## ${formato === 'desktop' ? 'Desktop' : 'Celular'}`, '',
      '| Tela | Desempenho | Acessib. | Boas práticas | FCP ms | LCP ms | CLS | TBT ms | Speed Index ms | Transferido KB |', '|---|---|---|---|---|---|---|---|---|---|');
    for (const r of resultados.filter(x => x.formato === formato)) {
      const m = r.mediana;
      l.push(`| ${r.tela} | ${m.desempenho} | ${m.acessibilidade} | ${m.boasPraticas} | ${m.fcp_ms} | ${m.lcp_ms} | ${m.cls} | ${m.tbt_ms} | ${m.si_ms} | ${m.transferencia_kb} |`);
    }
    l.push('');
  }
  const porAuditoria = new Map();
  for (const r of resultados) {
    for (const f of r.falhas ?? []) {
      const e = porAuditoria.get(f.id) ?? { titulo: f.titulo, categoria: f.categoria, telas: [] };
      e.telas.push(`${r.tela}/${r.formato === 'desktop' ? 'd' : 'c'}`);
      porAuditoria.set(f.id, e);
    }
  }
  l.push('## Auditorias com nota abaixo de 1 (acessibilidade e boas práticas)', '');
  if (!porAuditoria.size) {
    l.push('Nenhuma.');
  } else {
    l.push('| Auditoria | Categoria | Onde (d = desktop, c = celular) |', '|---|---|---|');
    for (const [id, e] of porAuditoria) l.push(`| \`${id}\` — ${e.titulo} | ${e.categoria} | ${e.telas.join(', ')} |`);
  }
  l.push('', '## Maiores oportunidades de desempenho (celular, rodada mediana)', '', '| Tela | Auditoria | Economia estimada ms |', '|---|---|---|');
  for (const r of resultados.filter(x => x.formato === 'mobile')) {
    for (const o of r.oportunidades ?? []) l.push(`| ${r.tela} | \`${o.id}\` — ${o.titulo} | ${o.economia_ms} |`);
  }
  l.push('', '## Deslocamentos de layout (telas com CLS acima de 0,05)', '', '| Tela | Formato | CLS | Elemento que se mexeu (pontuação) |', '|---|---|---|---|');
  for (const r of resultados.filter(x => x.mediana.cls > 0.05)) {
    const quem = (r.deslocamentos ?? []).map(d => `\`${d.elemento}\` (${d.pontuacao})`).join('; ') || 'não identificado';
    l.push(`| ${r.tela} | ${r.formato} | ${r.mediana.cls} | ${quem} |`);
  }
  return l.join('\n') + '\n';
}

const { proc, perfil } = await iniciarEdge();
const resultados = [];
let versaoEdge = 'desconhecida';
try {
  versaoEdge = (await (await fetch(`http://127.0.0.1:${PORTA}/json/version`)).json()).Browser;
  const browser = await puppeteer.connect({ browserURL: `http://127.0.0.1:${PORTA}` });
  mkdirSync(SAIDA, { recursive: true });

  // 1) tela de login, sem sessão
  for (const formato of FORMATOS) {
    const t = TELAS[0];
    resultados.push(await medir(browser, t, formato));
    console.log(`${t.id} (${formato}) ok`);
  }
  // 2) entra e mede as telas logadas
  await entrar(browser);
  for (const tela of TELAS.slice(1)) {
    for (const formato of FORMATOS) {
      resultados.push(await medir(browser, tela, formato));
      console.log(`${tela.id} (${formato}) ok`);
    }
  }
  browser.disconnect();
} finally {
  proc.kill();
  await dormir(500);
  try { rmSync(perfil, { recursive: true, force: true }); } catch { /* o Edge pode ainda segurar o perfil */ }
}

const meta = { data: new Date().toISOString().slice(0, 19), commit: process.env.METRICAS_COMMIT ?? 'sem-commit', edge: versaoEdge,
  lighthouse: (await import('lighthouse/package.json', { with: { type: 'json' } })).default.version };
writeFileSync(join(SAIDA, 'lighthouse.json'), JSON.stringify({ meta, resultados }, null, 2));
writeFileSync(join(SAIDA, 'lighthouse.md'), tabelaMd(resultados, meta));
console.log(`\nLighthouse: ${resultados.length} medições. Tabela: ${join(SAIDA, 'lighthouse.md')}`);
