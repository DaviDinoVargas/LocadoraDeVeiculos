using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Handlers;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Validators;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using LocadoraDeVeiculos.Core.Dominio.ModuloCondutor;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAluguel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloConfiguracao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCupom;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloTaxaServico;
using LocadoraDeVeiculos.Infraestrutura.Orm.ModuloPlanoCobranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.Compartilhado;

/// <summary>
/// Monta, em um banco InMemory já com tenant, tudo o que um aluguel precisa (grupo, plano de
/// cobrança, automóvel, cliente e condutor) e os repositórios reais. Evita repetir esse setup
/// em cada teste de aluguel. (A TestFixture antiga não serve: o DbContext dela não recebe
/// ITenantProvider e os filtros multi-tenant escondem as entidades.)
/// </summary>
public sealed class CenarioAluguel : IAsyncDisposable
{
    private sealed class TenantProviderFake : ITenantProvider
    {
        public Guid? EmpresaId { get; init; }
        public bool EstaNoCargo(string cargo) => false;
    }

    public Guid EmpresaId { get; } = Guid.NewGuid();
    public LocadoraDeVeiculosDbContext Contexto { get; private set; } = null!;
    public ITenantProvider TenantProvider { get; private set; } = null!;

    public RepositorioAluguelEmOrm RepositorioAluguel { get; private set; } = null!;
    public RepositorioAutomovelEmOrm RepositorioAutomovel { get; private set; } = null!;
    public RepositorioPlanoCobrancaEmOrm RepositorioPlanoCobranca { get; private set; } = null!;
    public RepositorioTaxaServicoEmOrm RepositorioTaxaServico { get; private set; } = null!;
    public RepositorioCupomEmOrm RepositorioCupom { get; private set; } = null!;
    public RepositorioConfiguracaoEmOrm RepositorioConfiguracao { get; private set; } = null!;

    public GrupoAutomovel Grupo { get; private set; } = null!;
    public PlanoCobranca Plano { get; private set; } = null!;
    public Automovel Automovel { get; private set; } = null!;
    public ClientePessoaFisica Cliente { get; private set; } = null!;
    public Condutor Condutor { get; private set; } = null!;

    public static async Task<CenarioAluguel> CriarAsync(decimal precoDiaria = 100m)
    {
        var cenario = new CenarioAluguel();
        await cenario.InicializarAsync(precoDiaria);
        return cenario;
    }

    private async Task InicializarAsync(decimal precoDiaria)
    {
        var options = new DbContextOptionsBuilder<LocadoraDeVeiculosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        TenantProvider = new TenantProviderFake { EmpresaId = EmpresaId };
        Contexto = new LocadoraDeVeiculosDbContext(options, TenantProvider);

        RepositorioAluguel = new RepositorioAluguelEmOrm(Contexto);
        RepositorioAutomovel = new RepositorioAutomovelEmOrm(Contexto);
        RepositorioPlanoCobranca = new RepositorioPlanoCobrancaEmOrm(Contexto);
        RepositorioTaxaServico = new RepositorioTaxaServicoEmOrm(Contexto);
        RepositorioCupom = new RepositorioCupomEmOrm(Contexto);
        RepositorioConfiguracao = new RepositorioConfiguracaoEmOrm(Contexto);

        Grupo = new GrupoAutomovel("Grupo Teste") { EmpresaId = EmpresaId };
        Contexto.Set<GrupoAutomovel>().Add(Grupo);

        Plano = new PlanoCobranca(Grupo.Id, EmpresaId, "Plano Padrão", precoDiaria: precoDiaria, precoPorKm: 0.5m, kmLivreLimite: 100);
        await RepositorioPlanoCobranca.CadastrarAsync(Plano);

        Automovel = await NovoAutomovelAsync("ABC1D23");

        Cliente = new ClientePessoaFisica(
            "Cliente Teste", "(11) 99999-9999", "cliente@teste.com", "Rua Teste, 1",
            "123.456.789-00", "12.345.678-9", "12345678900", DateTime.UtcNow.AddYears(2))
        {
            EmpresaId = EmpresaId
        };
        Contexto.Set<Cliente>().Add(Cliente);

        Condutor = new Condutor(
            "Condutor Teste", "condutor@teste.com", "111.222.333-44", "11122233344",
            DateTime.UtcNow.AddYears(1), "(11) 98888-8888", Cliente.Id)
        {
            EmpresaId = EmpresaId
        };
        Contexto.Set<Condutor>().Add(Condutor);

        await Contexto.SaveChangesAsync();
    }

    public async Task<Automovel> NovoAutomovelAsync(string placa)
    {
        var automovel = new Automovel(placa, "Fiat", "Prata", "Uno", TipoCombustivel.Gasolina, 50, 2020, null, Grupo.Id)
        {
            EmpresaId = EmpresaId
        };
        await RepositorioAutomovel.CadastrarAsync(automovel);
        await Contexto.SaveChangesAsync();
        return automovel;
    }

    public CadastrarAluguelCommandHandler CriarHandlerCadastro() => new(
        Contexto, RepositorioAluguel, RepositorioAutomovel, RepositorioPlanoCobranca,
        RepositorioTaxaServico, RepositorioCupom, RepositorioConfiguracao, TenantProvider,
        new CadastrarAluguelCommandValidator(),
        NullLogger<CadastrarAluguelCommandHandler>.Instance);

    public ValueTask DisposeAsync() => Contexto.DisposeAsync();
}
