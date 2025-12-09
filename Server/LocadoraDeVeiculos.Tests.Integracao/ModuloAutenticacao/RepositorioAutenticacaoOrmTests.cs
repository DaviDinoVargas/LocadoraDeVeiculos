using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloAutenticacao
{
    [TestClass]
    [TestCategory("Integração - Autenticação")]
    public sealed class RepositorioAutenticacaoOrmTests
    {
        private LocadoraDeVeiculosDbContext _context = null!;
        private UserManager<Usuario> _userManager = null!;
        private RoleManager<Cargo> _roleManager = null!;
        private AccessTokenProvider _accessTokenProvider = null!;
        private RefreshTokenProvider _refreshTokenProvider = null!;

        [TestInitialize]
        public void Inicializar()
        {
            var options = new DbContextOptionsBuilder<LocadoraDeVeiculosDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new LocadoraDeVeiculosDbContext(options);

            var storeUsuario = new UserStore<Usuario, Cargo, LocadoraDeVeiculosDbContext, Guid>(_context);
            var storeCargo = new RoleStore<Cargo, LocadoraDeVeiculosDbContext, Guid>(_context);

            var hasher = new PasswordHasher<Usuario>();

            _userManager = new UserManager<Usuario>(
                storeUsuario, null, hasher,
                new List<IUserValidator<Usuario>>(),
                new List<IPasswordValidator<Usuario>>(),
                null, null, null, null
            );

            _roleManager = new RoleManager<Cargo>(
                storeCargo, null, null, null, null
            );

            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "JWT_GENERATION_KEY", "CHAVE_SUPER_SECRETA_DE_TESTE_123" },
                    { "JWT_AUDIENCE_DOMAIN", "localhost" }
                })
                .Build();

            var httpContext = new DefaultHttpContext();
            var accessor = new HttpContextAccessor { HttpContext = httpContext };

            _accessTokenProvider = new AccessTokenProvider(_context, _userManager, config);
            _refreshTokenProvider = new RefreshTokenProvider(_context, _userManager, accessor);
        }

        [TestMethod]
        public async Task FluxoCompleto_DeveFuncionar()
        {
            // 1) REGISTRAR USUÁRIO
            var registrarHandler = new RegistrarUsuarioCommandHandler(
                _userManager,
                _roleManager,
                _accessTokenProvider,
                _refreshTokenProvider,
                NullLogger<RegistrarUsuarioCommandHandler>.Instance
            );

            var registrarCommand = new RegistrarUsuarioCommand(
                NomeCompleto: "Usuário Teste",
                Email: "teste@teste.com",
                Senha: "Senha@123",
                ConfirmarSenha: "Senha@123"
            );

            var registrarResult = await registrarHandler.Handle(registrarCommand, CancellationToken.None);

            Assert.IsTrue(registrarResult.IsSuccess);
            Assert.IsNotNull(registrarResult.Value.Item1);
            Assert.IsNotNull(registrarResult.Value.Item2);

            // 2) CONFIRMAR CRIAÇÃO DE USUÁRIO
            var usuarioCriado = await _userManager.FindByEmailAsync("teste@teste.com");
            Assert.IsNotNull(usuarioCriado);

            // 3) VALIDAR SENHA
            var senhaValida = await _userManager.CheckPasswordAsync(usuarioCriado!, "Senha@123");
            Assert.IsTrue(senhaValida);

            // 4) GERAR TOKENS MANUALMENTE
            var accessToken = await _accessTokenProvider.GerarAccessTokenAsync(usuarioCriado!);
            var refreshToken = await _refreshTokenProvider.GerarRefreshTokenAsync(usuarioCriado!);

            Assert.IsFalse(string.IsNullOrEmpty(accessToken.Chave));
            Assert.IsFalse(string.IsNullOrEmpty(refreshToken.TokenHash));

            // 5) ROTACIONAR REFRESH TOKEN
            var rotacionarHandler = new RotacionarTokenCommandHandler(
                _context,
                _accessTokenProvider,
                _refreshTokenProvider,
                NullLogger<RotacionarTokenCommandHandler>.Instance
            );

            var rotacionarCommand = new RotacionarTokenCommand(refreshToken.TokenHash);
            var rotacaoResult = await rotacionarHandler.Handle(rotacionarCommand, CancellationToken.None);

            Assert.IsTrue(rotacaoResult.IsSuccess);
            Assert.IsNotNull(rotacaoResult.Value.Item1);
            Assert.IsNotNull(rotacaoResult.Value.Item2);

            // 6) GARANTIR QUE TOKEN ANTIGO FOI REVOGADO
            var tokenAntigo = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == refreshToken.TokenHash);
            Assert.IsNotNull(tokenAntigo);
            Assert.IsNotNull(tokenAntigo.RevogadoEmUtc);
        }
    }
}