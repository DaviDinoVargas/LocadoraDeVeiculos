// Configuração do Playwright. Roda contra o build de PRODUÇÃO servido pelo orquestrar.py, no Edge instalado
// (channel 'msedge': não baixa navegador). Um worker só: os fluxos compartilham a empresa de teste e o refresh token
// da sessão é rotativo (duas sessões em paralelo com o mesmo login se atrapalhariam).
import { join } from 'node:path';
import { defineConfig } from '@playwright/test';

const saida = join(process.env.METRICAS_SAIDA ?? '.', 'e2e');

export default defineConfig({
  testDir: '.',
  testMatch: '*.spec.mjs',
  outputDir: join(saida, 'artefatos'),
  workers: 1,
  fullyParallel: false,
  retries: 0,
  timeout: 90_000,
  expect: { timeout: 10_000 },
  reporter: [['list'], ['json', { outputFile: join(saida, 'playwright.json') }]],
  use: {
    baseURL: process.env.METRICAS_URL,
    channel: 'msedge',
    headless: true,
    locale: 'pt-BR',
    timezoneId: 'America/Sao_Paulo',
    viewport: { width: 1366, height: 768 },
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure'
  }
});
