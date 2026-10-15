// axe-core na PÁGINA INTEIRA (landmarks, título, idioma, contraste) nas telas-chave, nos temas claro e escuro,
// já com a sessão aberta. Complementa o auditor de componentes do Karma.
import { test } from '@playwright/test';
import { ALUGUEIS, entrar, novoIp, semViolacoes } from './apoio.mjs';

const emAndamento = ALUGUEIS.em_andamento?.[0];
const TELAS = [
  ['/home', 'home'], ['/clientes', 'clientes'], ['/condutores', 'condutores'], ['/veiculos', 'veículos'], ['/alugueis', 'aluguéis'],
  ['/devolucoes', 'devoluções'], ['/alugueis/new', 'novo aluguel'], ['/veiculos/new', 'novo veículo'], ['/clientes/new/pf', 'novo cliente'],
  ...(emAndamento ? [[`/alugueis/${emAndamento}/devolver`, 'devolução']] : [])
];

for (const esquema of ['light', 'dark']) {
  test(`telas-chave sem violações de acessibilidade — tema ${esquema === 'light' ? 'claro' : 'escuro'}`, async ({ page }) => {
    await page.emulateMedia({ colorScheme: esquema });
    // login (sem sessão) primeiro; depois entra e percorre as telas
    await novoIp(page);
    await page.goto('/login');
    await page.locator('input[type="email"]').waitFor();
    await semViolacoes(page, `login (${esquema})`);
    await entrar(page);
    for (const [caminho, nome] of TELAS) {
      await novoIp(page);   // cada recarga renova o token e consome o limite de autenticação do IP
      await page.goto(caminho);
      await page.locator('main').first().waitFor();
      await page.waitForLoadState('networkidle');
      await semViolacoes(page, `${nome} (${esquema})`);
    }
  });
}
