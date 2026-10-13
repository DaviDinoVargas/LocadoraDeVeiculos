using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;

/// <summary>Um refresh token já trocado voltou a ser apresentado: sessões derrubadas. Carrega o dono para a auditoria.</summary>
public class ReusoDeRefreshTokenException(Guid usuarioId) : SecurityTokenException("Token de rotação já utilizado/revogado (suspeita de reuso).")
{
    public Guid UsuarioId { get; } = usuarioId;
}

public class RefreshTokenProvider(
    LocadoraDeVeiculosDbContext dbContext,
    UserManager<Usuario> userManager,
    IHttpContextAccessor contextAccessor
)
{
    private readonly HttpContext? httpContext = contextAccessor.HttpContext;
    private readonly TimeSpan expiracaoTokenEmDias = TimeSpan.FromDays(7);

    private const string MotivoRotacao = "Rotação";

    // Tokens vencidos/revogados guardam IP e User-Agent (dados pessoais): depois deste prazo não têm mais utilidade.
    private static readonly TimeSpan RetencaoDeTokensEncerrados = TimeSpan.FromDays(30);

    public async Task<(RefreshToken entidade, string tokenBruto)> GerarRefreshTokenAsync(Usuario usuario)
    {
        var tokenBruto = GerarTokenOpaco();

        var hash = Hash(tokenBruto);

        var now = DateTime.UtcNow;

        var novoRefreshToken = new RefreshToken
        {
            UsuarioId = usuario.Id,
            TokenHash = hash,
            CriadoEmUtc = now,
            ExpiraEmUtc = now.Add(expiracaoTokenEmDias),
            IpCriacao = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers[HeaderNames.UserAgent].ToString()
        };

        dbContext.RefreshTokens.Add(novoRefreshToken);

        // Limpeza de tokens antigos do usuário na própria emissão (a tabela não cresce para sempre).
        var limite = now - RetencaoDeTokensEncerrados;
        var antigos = await dbContext.RefreshTokens
            .Where(t => t.UsuarioId == usuario.Id && t.Id != novoRefreshToken.Id && t.ExpiraEmUtc < limite)
            .ToListAsync();
        dbContext.RefreshTokens.RemoveRange(antigos);

        await dbContext.SaveChangesAsync();

        // Só o token BRUTO deve sair do servidor (via cookie). O banco guarda apenas o hash:
        // se o banco vazar, o valor exposto não é suficiente para autenticar (precisaria reverter o SHA-256).
        return (novoRefreshToken, tokenBruto);
    }

    public async Task<(Usuario usuario, RefreshToken novoRefreshToken, string novoTokenBruto)> RotacionarRefreshTokenAsync(string refreshTokenBruto)
    {
        var hashRecebido = Hash(refreshTokenBruto);

        var token = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hashRecebido);

        if (token is null)
            throw new SecurityTokenException("Token de rotação não foi encontado.");

        if (token.RevogadoEmUtc is not null)
        {
            // Um token que já foi trocado por outro e volta a ser apresentado só pode ter sido copiado (cookie roubado ou
            // repetido): quem tem o token novo e quem tem o velho são pessoas diferentes e não dá para saber qual é o
            // dono legítimo. A resposta segura é derrubar todas as sessões desse usuário.
            if (token.MotivoRevogacao == MotivoRotacao)
            {
                await DerrubarSessoesPorReusoAsync(token.UsuarioId);

                throw new ReusoDeRefreshTokenException(token.UsuarioId);
            }

            throw new SecurityTokenException("Token de rotação já revogado.");
        }

        if (token.ExpiraEmUtc <= DateTime.UtcNow)
            throw new SecurityTokenException("Token de rotação expirado.");

        // carrega usuário
        var usuario = await userManager.FindByIdAsync(token.UsuarioId.ToString())
            ?? throw new SecurityTokenException("Usuário não encontrado.");

        // cria novo token e revoga o antigo
        var novoTokenBruto = GerarTokenOpaco();

        var novoHash = Hash(novoTokenBruto);

        var now = DateTime.UtcNow;

        if (dbContext.Database.IsRelational())
        {
            // Rotação atômica: o UPDATE só vale se o token ainda não foi revogado. Duas chamadas simultâneas com o mesmo
            // cookie não conseguem as duas: a segunda afeta 0 linhas e é tratada como reuso.
            var afetadas = await dbContext.RefreshTokens
                .Where(t => t.Id == token.Id && t.RevogadoEmUtc == null)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(t => t.RevogadoEmUtc, now)
                    .SetProperty(t => t.SubstituidoPorTokenHash, novoHash)
                    .SetProperty(t => t.MotivoRevogacao, MotivoRotacao));

            if (afetadas == 0)
            {
                await DerrubarSessoesPorReusoAsync(token.UsuarioId);

                throw new ReusoDeRefreshTokenException(token.UsuarioId);
            }
        }
        else
        {
            token.RevogadoEmUtc = now;
            token.SubstituidoPorTokenHash = novoHash;
            token.MotivoRevogacao = MotivoRotacao;
        }

        var novoRefreshToken = new RefreshToken
        {
            UsuarioId = usuario.Id,
            TokenHash = novoHash,
            CriadoEmUtc = now,
            ExpiraEmUtc = now.Add(expiracaoTokenEmDias),
            IpCriacao = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers[HeaderNames.UserAgent].ToString()
        };

        await dbContext.RefreshTokens.AddAsync(novoRefreshToken);

        await dbContext.SaveChangesAsync();

        return (usuario, novoRefreshToken, novoTokenBruto);
    }

    /// <summary>
    /// Resolve o dono de um refresh token a partir do valor BRUTO recebido do cliente (cookie),
    /// aplicando o mesmo hash usado no cadastro antes de consultar o banco. Usado no logout.
    /// </summary>
    public async Task<Guid?> ObterUsuarioIdPorTokenBrutoAsync(string refreshTokenBruto)
    {
        var hash = Hash(refreshTokenBruto);

        var token = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        return token?.UsuarioId;
    }

    private async Task DerrubarSessoesPorReusoAsync(Guid usuarioId)
    {
        await RevogarTokensUsuarioAsync(usuarioId, "Reuso detectado");

        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());

        if (usuario is not null)
        {
            usuario.AccessTokenVersionId = Guid.NewGuid();
            await userManager.UpdateAsync(usuario);
        }
    }

    public async Task RevogarTokensUsuarioAsync(Guid usuarioId, string motivo)
    {
        var tokensAtivos = await dbContext.RefreshTokens
            .Where(t =>
                t.UsuarioId == usuarioId &&
                t.RevogadoEmUtc == null &&
                t.ExpiraEmUtc > DateTime.UtcNow
            )
            .ToListAsync();

        foreach (var t in tokensAtivos)
        {
            t.RevogadoEmUtc = DateTime.UtcNow;
            t.MotivoRevogacao = motivo;
        }

        await dbContext.SaveChangesAsync();
    }

    private static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));

        return Convert.ToBase64String(bytes);
    }

    private static string GerarTokenOpaco()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);

        return Convert.ToBase64String(bytes);
    }
}
