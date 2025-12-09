using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using LocadoraDeVeiculos.Core.Dominio.ModuloCondutor;
using LocadoraDeVeiculos.Core.Dominio.ModuloFuncionario;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloFuncionario;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCliente;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAluguel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloTaxaServico;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloDevolucao;

namespace LocadoraDeVeiculos.Tests.Integracao.Compartilhado
{
    [TestClass]
    public class TestFixture
    {
        protected ServiceProvider serviceProvider;
        protected LocadoraDeVeiculosDbContext dbContext;

        protected UserManager<Usuario> userManager;
        protected RoleManager<Cargo> roleManager;

        protected RepositorioFuncionarioEmOrm repositorioFuncionario;
        protected RepositorioClienteEmOrm repositorioCliente;
        protected RepositorioAutomovelEmOrm repositorioAutomovel;
        protected RepositorioGrupoAutomovelEmOrm repositorioGrupoAutomovel;
        protected RepositorioPlanoCobrancaEmOrm repositorioPlanoCobranca;
        protected RepositorioAluguelEmOrm repositorioAluguel;
        protected RepositorioDevolucaoEmOrm repositorioDevolucao;
        protected RepositorioTaxaServicoEmOrm repositorioTaxaServico;

        [TestInitialize]
        public async Task Setup()
        {
            var services = new ServiceCollection();

            // Configura banco em memória
            services.AddDbContext<LocadoraDeVeiculosDbContext>(opt =>
                opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));

            // Identity
            services.AddIdentity<Usuario, Cargo>()
                .AddEntityFrameworkStores<LocadoraDeVeiculosDbContext>()
                .AddDefaultTokenProviders();

            serviceProvider = services.BuildServiceProvider();

            dbContext = serviceProvider.GetRequiredService<LocadoraDeVeiculosDbContext>();
            userManager = serviceProvider.GetRequiredService<UserManager<Usuario>>();
            roleManager = serviceProvider.GetRequiredService<RoleManager<Cargo>>();

            await CriarCargoPadrao();

            // Inicializa repositórios
            repositorioFuncionario = new RepositorioFuncionarioEmOrm(dbContext);
            repositorioCliente = new RepositorioClienteEmOrm(dbContext);
            repositorioAutomovel = new RepositorioAutomovelEmOrm(dbContext);
            repositorioGrupoAutomovel = new RepositorioGrupoAutomovelEmOrm(dbContext);
            repositorioPlanoCobranca = new RepositorioPlanoCobrancaEmOrm(dbContext);
            repositorioAluguel = new RepositorioAluguelEmOrm(dbContext);
            repositorioDevolucao = new RepositorioDevolucaoEmOrm(dbContext);
            repositorioTaxaServico = new RepositorioTaxaServicoEmOrm(dbContext);
        }

        private async Task CriarCargoPadrao()
        {
            if (!await roleManager.RoleExistsAsync("Administrador"))
            {
                await roleManager.CreateAsync(new Cargo
                {
                    Id = Guid.NewGuid(),
                    Name = "Administrador",
                    NormalizedName = "ADMINISTRADOR"
                });
            }
        }

        protected async Task<Usuario> CriarUsuarioAsync(
            string email = "usuario@teste.com",
            string senha = "Senha@123")
        {
            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                NormalizedUserName = email.ToUpper(),
                NormalizedEmail = email.ToUpper(),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var result = await userManager.CreateAsync(usuario, senha);

            if (!result.Succeeded)
                throw new Exception("Falha ao criar usuário de teste");

            await userManager.AddToRoleAsync(usuario, "Administrador");

            return usuario;
        }

        protected async Task<Funcionario> CriarFuncionarioAsync(
            string nome = "Funcionário Teste",
            string email = "funcionario@teste.com",
            decimal salario = 3000)
        {
            var usuario = await CriarUsuarioAsync(email);

            var funcionario = new Funcionario(
                usuarioId: usuario.Id,
                tenantId: Guid.NewGuid(),
                nomeCompleto: nome,
                cpf: "12345678900",
                email: email,
                salario: salario,
                admissaoEmUtc: DateTimeOffset.UtcNow
            );

            await repositorioFuncionario.CadastrarAsync(funcionario);
            await dbContext.SaveChangesAsync();

            return funcionario;
        }
    }
}
