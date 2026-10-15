// Fluxos críticos de ponta a ponta, pela interface, contra o build de produção e o banco de métricas.
// Cada fluxo é cronometrado (tempo, cliques, preenchimentos) por `tarefa()`; os tempos viram a linha de base automatizada
// das tarefas de usabilidade. O terceiro fluxo também prova, de ponta a ponta, o conserto da lista de devoluções
// (condutor e cliente apareciam como "N/A").
import { expect, test } from '@playwright/test';
import { ALUGUEIS, campo, clicar, cpfValido, dataIso, entrar, novoIp, preencher, tarefa } from './apoio.mjs';

test.describe('Fluxos críticos', () => {
  test('1. cadastrar um cliente pessoa física e vê-lo na lista', async ({ page }) => {
    await entrar(page);
    const nome = `Cliente E2E ${Date.now().toString().slice(-6)}`;

    await tarefa('Cadastrar cliente (pessoa física)', async a => {
      await novoIp(page);
      await page.goto('/clientes');
      await clicar(a, page.getByRole('button', { name: /novo cliente/i }).first());
      await clicar(a, page.getByRole('menuitem', { name: /pessoa física/i }));
      await expect(page).toHaveURL(/clientes\/new\/pf/);

      await preencher(a, campo(page, 'Nome completo'), nome);
      await preencher(a, campo(page, 'CPF'), cpfValido());
      await preencher(a, campo(page, 'RG'), '12.345.678-9');
      await preencher(a, campo(page, 'CNH'), '12345678901');
      await preencher(a, campo(page, 'Validade da CNH'), dataIso(900));
      await preencher(a, campo(page, 'Telefone'), '11999990000');
      await preencher(a, campo(page, 'E-mail'), `e2e${Date.now()}@teste.local`);
      await preencher(a, campo(page, 'Endereço'), 'Rua dos Testes, 100, Centro');
      await clicar(a, page.getByRole('button', { name: 'Criar cliente' }));

      await expect(page).toHaveURL(/\/clientes$/);
      await expect(page.getByText(nome).first()).toBeVisible();
    });
  });

  test('2. criar um aluguel (reserva) e vê-lo como Reservado na lista', async ({ page }) => {
    await entrar(page);

    await tarefa('Criar aluguel', async a => {
      await novoIp(page);
      await page.goto('/alugueis');
      await clicar(a, page.getByRole('link', { name: /novo aluguel/i }).or(page.getByRole('button', { name: /novo aluguel/i })).first());
      await expect(page).toHaveURL(/alugueis\/new/);

      await campo(page, 'Cliente').selectOption({ label: 'Ana Souza' });
      a.cliques++;
      await expect(campo(page, 'Condutor')).toBeEnabled();
      await campo(page, 'Condutor').selectOption({ index: 1 });
      a.cliques++;
      await preencher(a, campo(page, 'Data de saída'), dataIso(40));
      await preencher(a, campo(page, 'Data de retorno previsto'), dataIso(43));
      // os veículos disponíveis dependem do período: espera a lista carregar e escolhe o primeiro
      await expect.poll(async () => campo(page, 'Veículo').locator('option').count()).toBeGreaterThan(1);
      await campo(page, 'Veículo').selectOption({ index: 1 });
      a.cliques++;
      await clicar(a, page.getByRole('button', { name: 'Criar aluguel' }));

      await expect(page).toHaveURL(/\/alugueis$/);
      await expect(page.getByText('Reservado').first()).toBeVisible();
    });
  });

  test('3. registrar uma devolução e ver condutor e cliente na lista de devoluções', async ({ page }) => {
    const id = ALUGUEIS.em_andamento?.at(-1);
    test.skip(!id, 'a semeadura não criou aluguel em andamento');
    await entrar(page);

    await tarefa('Registrar devolução', async a => {
      await novoIp(page);
      await page.goto(`/alugueis/${id}/devolver`);
      await preencher(a, campo(page, 'Data da devolução'), dataIso(12));
      await preencher(a, campo(page, 'Quilometragem final'), '15000');
      await campo(page, 'Nível de combustível').selectOption({ index: 1 });
      a.cliques++;
      await preencher(a, campo(page, 'Combustível no tanque'), '40');
      await clicar(a, page.getByRole('button', { name: 'Registrar devolução' }));

      await expect(page).toHaveURL(/devolucoes/);
    });

    await novoIp(page);
    await page.goto('/devolucoes');
    await page.waitForLoadState('networkidle');      // a lista começa em esqueleto; só vale ler depois que os dados chegam
    const linhas = page.locator('tbody tr');
    await expect(linhas.first()).toBeVisible();
    await expect(linhas.first().locator('td').nth(1)).not.toBeEmpty();
    const texto = (await linhas.allTextContents()).join('\n');
    expect(texto, 'a lista de devoluções não pode mostrar N/A em condutor, cliente ou placa').not.toContain('N/A');
    expect(texto).toContain('Condutor ');
  });
});
