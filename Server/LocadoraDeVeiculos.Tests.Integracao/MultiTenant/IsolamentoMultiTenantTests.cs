using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCliente;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.MultiTenant;

/// <summary>
/// Regressão para a falha crítica de isolamento multi-tenant (IDOR): antes desta correção,
/// o filtro por EmpresaId só existia para Funcionario, então qualquer usuário autenticado
/// conseguia ler/editar/excluir dados de OUTRAS empresas em praticamente todos os módulos.
/// Estes testes garantem que a Empresa B nunca enxerga, edita ou exclui dados da Empresa A,
/// usando o filtro global aplicado em LocadoraDeVeiculosDbContext.OnModelCreating.
/// </summary>
[TestClass]
[TestCategory("Integração - Multi-tenant")]
public sealed class IsolamentoMultiTenantTests
{
    private sealed class TenantProviderFake : ITenantProvider
    {
        public Guid? EmpresaId { get; init; }
        public bool EstaNoCargo(string cargo) => false;
    }

    private static LocadoraDeVeiculosDbContext CriarContexto(string nomeBanco, Guid? empresaId)
    {
        var options = new DbContextOptionsBuilder<LocadoraDeVeiculosDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;

        return new LocadoraDeVeiculosDbContext(options, new TenantProviderFake { EmpresaId = empresaId });
    }

    [TestMethod]
    public async Task EmpresaB_NaoDeveVerEditarNemExcluirAutomovelDaEmpresaA()
    {
        var nomeBanco = Guid.NewGuid().ToString();
        var empresaA = Guid.NewGuid();
        var empresaB = Guid.NewGuid();
        var automovelId = Guid.NewGuid();

        await using (var contextoA = CriarContexto(nomeBanco, empresaA))
        {
            // GrupoAutomovel é uma FK obrigatória: precisa existir e pertencer à mesma
            // empresa, senão o Include() da consulta vira INNER JOIN e não acha o registro.
            var grupo = new GrupoAutomovel("Grupo Teste") { EmpresaId = empresaA };
            contextoA.Set<GrupoAutomovel>().Add(grupo);

            var automovel = new Automovel("ABC1234", "Fiat", "Prata", "Uno", TipoCombustivel.Gasolina, 50, 2020, null, grupo.Id)
            {
                Id = automovelId,
                EmpresaId = empresaA
            };

            contextoA.Set<Automovel>().Add(automovel);
            await contextoA.SaveChangesAsync();
        }

        await using (var contextoB = CriarContexto(nomeBanco, empresaB))
        {
            var repositorioB = new RepositorioAutomovelEmOrm(contextoB);

            Assert.IsNull(
                await repositorioB.SelecionarPorIdAsync(automovelId),
                "Empresa B não pode conseguir o automóvel da Empresa A por Id.");

            var listaDaEmpresaB = await repositorioB.SelecionarTodosAsync();
            Assert.IsFalse(
                listaDaEmpresaB.Any(a => a.Id == automovelId),
                "A listagem da Empresa B não pode incluir automóveis da Empresa A.");

            Assert.IsFalse(
                await repositorioB.ExcluirAsync(automovelId),
                "Empresa B não pode excluir um automóvel que pertence à Empresa A.");
        }

        await using (var contextoA2 = CriarContexto(nomeBanco, empresaA))
        {
            var repositorioA = new RepositorioAutomovelEmOrm(contextoA2);

            Assert.IsNotNull(
                await repositorioA.SelecionarPorIdAsync(automovelId),
                "A própria Empresa A deve continuar enxergando seu automóvel normalmente.");
        }
    }

    [TestMethod]
    public async Task EmpresaB_NaoDeveVerClienteDaEmpresaA_MesmoSendoEntidadeComHeranca()
    {
        // Cliente é TPH (ClientePessoaFisica/ClientePessoaJuridica). O filtro é aplicado na
        // raiz (Cliente); este teste garante que ele também vale para os tipos derivados.
        var nomeBanco = Guid.NewGuid().ToString();
        var empresaA = Guid.NewGuid();
        var empresaB = Guid.NewGuid();
        var clienteId = Guid.NewGuid();

        await using (var contextoA = CriarContexto(nomeBanco, empresaA))
        {
            var cliente = new ClientePessoaFisica(
                "Cliente da Empresa A", "11999990000", "cliente@empresaA.com", "Rua A, 123",
                "123.456.789-00", "12.345.678-9", "12345678900", DateTime.UtcNow.AddYears(2))
            {
                Id = clienteId,
                EmpresaId = empresaA
            };

            contextoA.Set<Cliente>().Add(cliente);
            await contextoA.SaveChangesAsync();
        }

        await using (var contextoB = CriarContexto(nomeBanco, empresaB))
        {
            var repositorioB = new RepositorioClienteEmOrm(contextoB);

            Assert.IsNull(
                await repositorioB.SelecionarPorIdAsync(clienteId),
                "Empresa B não pode conseguir o cliente da Empresa A por Id.");

            var listaDaEmpresaB = await repositorioB.SelecionarTodosAsync();
            Assert.IsFalse(
                listaDaEmpresaB.Any(c => c.Id == clienteId),
                "A listagem da Empresa B não pode incluir clientes da Empresa A.");
        }
    }

    [TestMethod]
    public async Task SemTenantNoContexto_NaoDeveVazarDadosDeNenhumaEmpresa()
    {
        // Simula uma chamada sem usuário autenticado no HttpContext (tenantProvider.EmpresaId == null):
        // o filtro deve fechar por padrão (nenhum dado visível), nunca abrir para "ver tudo".
        var nomeBanco = Guid.NewGuid().ToString();
        var empresaA = Guid.NewGuid();

        await using (var contextoA = CriarContexto(nomeBanco, empresaA))
        {
            var grupo = new GrupoAutomovel("Grupo Teste") { EmpresaId = empresaA };
            contextoA.Set<GrupoAutomovel>().Add(grupo);

            var automovel = new Automovel("XYZ9999", "VW", "Branco", "Gol", TipoCombustivel.Flex, 50, 2021, null, grupo.Id)
            {
                EmpresaId = empresaA
            };

            contextoA.Set<Automovel>().Add(automovel);
            await contextoA.SaveChangesAsync();
        }

        await using var contextoSemTenant = CriarContexto(nomeBanco, empresaId: null);
        var repositorio = new RepositorioAutomovelEmOrm(contextoSemTenant);

        var resultado = await repositorio.SelecionarTodosAsync();

        Assert.AreEqual(0, resultado.Count, "Sem tenant resolvido, nenhum registro de nenhuma empresa deve ser retornado.");
    }
}
