using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Handlers;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Validators;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using LocadoraDeVeiculos.Core.Dominio.ModuloCondutor;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAluguel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCupom;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloTaxaServico;
using LocadoraDeVeiculos.Infraestrutura.Orm.ModuloPlanoCobranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloAluguel;

/// <summary>
/// Testa o fluxo completo (handler real, não só a query do repositório) de cadastrar um
/// aluguel informando um código de cupom: o desconto deve ser aplicado sobre o valor
/// calculado pelo servidor, e o uso do cupom deve ser contabilizado.
/// </summary>
[TestClass]
[TestCategory("Integração - Aluguel com cupom")]
public sealed class CadastrarAluguelComCupomTests
{
    private sealed class TenantProviderFake : ITenantProvider
    {
        public Guid? EmpresaId { get; init; }
        public bool EstaNoCargo(string cargo) => false;
    }

    [TestMethod]
    public async Task CadastrarAluguel_ComCupomValido_DeveAplicarDescontoERegistrarUso()
    {
        var empresaId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<LocadoraDeVeiculosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var contexto = new LocadoraDeVeiculosDbContext(options, new TenantProviderFake { EmpresaId = empresaId });

        var repositorioAluguel = new RepositorioAluguelEmOrm(contexto);
        var repositorioAutomovel = new RepositorioAutomovelEmOrm(contexto);
        var repositorioPlanoCobranca = new RepositorioPlanoCobrancaEmOrm(contexto);
        var repositorioTaxaServico = new RepositorioTaxaServicoEmOrm(contexto);
        var repositorioCupom = new RepositorioCupomEmOrm(contexto);
        var tenantProvider = new TenantProviderFake { EmpresaId = empresaId };

        // Massa de dados: grupo -> plano de cobrança (diária de 100) -> automóvel no grupo
        var grupo = new GrupoAutomovel("Grupo Teste") { EmpresaId = empresaId };
        contexto.Set<GrupoAutomovel>().Add(grupo);

        var plano = new PlanoCobranca(grupo.Id, empresaId, "Plano Padrão", precoDiaria: 100m, precoPorKm: 0.5m, kmLivreLimite: 100);
        await repositorioPlanoCobranca.CadastrarAsync(plano);

        var automovel = new Automovel("ABC1D23", "Fiat", "Prata", "Uno", TipoCombustivel.Gasolina, 50, 2020, null, grupo.Id)
        {
            EmpresaId = empresaId
        };
        await repositorioAutomovel.CadastrarAsync(automovel);

        var cliente = new ClientePessoaFisica(
            "Cliente Teste", "(11) 99999-9999", "cliente@teste.com", "Rua Teste, 1",
            "123.456.789-00", "12.345.678-9", "12345678900", DateTime.UtcNow.AddYears(2))
        {
            EmpresaId = empresaId
        };
        contexto.Set<Cliente>().Add(cliente);

        var condutor = new Condutor(
            "Condutor Teste", "condutor@teste.com", "111.222.333-44", "11122233344",
            DateTime.UtcNow.AddYears(1), "(11) 98888-8888", cliente.Id)
        {
            EmpresaId = empresaId
        };
        contexto.Set<Condutor>().Add(condutor);

        var cupom = new Cupom("DESCONTO20", "20% de desconto", TipoDesconto.Percentual, 20m,
            DateTimeOffset.UtcNow.AddDays(30), limiteUsos: 5, parceiroId: null)
        {
            EmpresaId = empresaId
        };
        await repositorioCupom.CadastrarAsync(cupom);

        await contexto.SaveChangesAsync();

        var handler = new CadastrarAluguelCommandHandler(
            contexto, repositorioAluguel, repositorioAutomovel, repositorioPlanoCobranca,
            repositorioTaxaServico, repositorioCupom, tenantProvider,
            new CadastrarAluguelCommandValidator(),
            NullLogger<CadastrarAluguelCommandHandler>.Instance);

        var dataSaida = DateTimeOffset.UtcNow.AddHours(1);
        var dataRetorno = dataSaida.AddDays(3); // 3 diárias * 100 = 300 bruto

        var command = new CadastrarAluguelCommand(
            condutor.Id, automovel.Id, cliente.Id, dataSaida, dataRetorno,
            ValorPrevisto: 1m, // valor mandado pelo "cliente" -- tem que ser ignorado
            TaxasServicosIds: new(),
            CupomCodigo: "desconto20" // minúsculo, de propósito -- deve normalizar
        );

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess, resultado.IsFailed ? string.Join("; ", resultado.Errors) : "");

        var aluguelCriado = await repositorioAluguel.SelecionarAluguelCompletoPorIdAsync(resultado.Value.Id);

        Assert.IsNotNull(aluguelCriado);
        Assert.AreEqual(cupom.Id, aluguelCriado!.CupomId);
        Assert.AreEqual(60m, aluguelCriado.ValorDesconto);   // 20% de 300
        Assert.AreEqual(240m, aluguelCriado.ValorPrevisto);  // 300 - 60

        var cupomAtualizado = await repositorioCupom.SelecionarPorIdAsync(cupom.Id);
        Assert.AreEqual(1, cupomAtualizado!.UsosAtuais);
    }

    [TestMethod]
    public async Task CadastrarAluguel_ComCupomExpirado_DeveSerRejeitado()
    {
        var empresaId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<LocadoraDeVeiculosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var contexto = new LocadoraDeVeiculosDbContext(options, new TenantProviderFake { EmpresaId = empresaId });

        var repositorioAluguel = new RepositorioAluguelEmOrm(contexto);
        var repositorioAutomovel = new RepositorioAutomovelEmOrm(contexto);
        var repositorioPlanoCobranca = new RepositorioPlanoCobrancaEmOrm(contexto);
        var repositorioTaxaServico = new RepositorioTaxaServicoEmOrm(contexto);
        var repositorioCupom = new RepositorioCupomEmOrm(contexto);
        var tenantProvider = new TenantProviderFake { EmpresaId = empresaId };

        var grupo = new GrupoAutomovel("Grupo Teste") { EmpresaId = empresaId };
        contexto.Set<GrupoAutomovel>().Add(grupo);

        var plano = new PlanoCobranca(grupo.Id, empresaId, "Plano Padrão", 100m, 0.5m, 100);
        await repositorioPlanoCobranca.CadastrarAsync(plano);

        var automovel = new Automovel("XYZ9A88", "VW", "Branco", "Gol", TipoCombustivel.Flex, 50, 2021, null, grupo.Id)
        {
            EmpresaId = empresaId
        };
        await repositorioAutomovel.CadastrarAsync(automovel);

        var cliente = new ClientePessoaFisica(
            "Cliente Teste 2", "(11) 97777-7777", "cliente2@teste.com", "Rua Teste, 2",
            "999.888.777-66", "98.765.432-1", "99988877766", DateTime.UtcNow.AddYears(2))
        {
            EmpresaId = empresaId
        };
        contexto.Set<Cliente>().Add(cliente);

        var condutor = new Condutor(
            "Condutor Teste 2", "condutor2@teste.com", "555.666.777-88", "55566677788",
            DateTime.UtcNow.AddYears(1), "(11) 96666-6666", cliente.Id)
        {
            EmpresaId = empresaId
        };
        contexto.Set<Condutor>().Add(condutor);

        var cupomExpirado = new Cupom("VENCIDO", "Cupom vencido", TipoDesconto.Percentual, 10m,
            DateTimeOffset.UtcNow.AddDays(-1), limiteUsos: null, parceiroId: null)
        {
            EmpresaId = empresaId
        };
        await repositorioCupom.CadastrarAsync(cupomExpirado);

        await contexto.SaveChangesAsync();

        var handler = new CadastrarAluguelCommandHandler(
            contexto, repositorioAluguel, repositorioAutomovel, repositorioPlanoCobranca,
            repositorioTaxaServico, repositorioCupom, tenantProvider,
            new CadastrarAluguelCommandValidator(),
            NullLogger<CadastrarAluguelCommandHandler>.Instance);

        var dataSaida = DateTimeOffset.UtcNow.AddHours(1);

        var command = new CadastrarAluguelCommand(
            condutor.Id, automovel.Id, cliente.Id, dataSaida, dataSaida.AddDays(2),
            ValorPrevisto: 1m, TaxasServicosIds: new(), CupomCodigo: "VENCIDO");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
    }
}
