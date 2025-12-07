using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;

public class AccessTokenProvider
{
    private readonly LocadoraDeVeiculosDbContext dbContext;
    private readonly UserManager<Usuario> userManager;

    private readonly string audienciaValida;
    private readonly string chaveAssinaturaJwt;

    public AccessTokenProvider(
        LocadoraDeVeiculosDbContext dbContext,
        UserManager<Usuario> userManager,
        IConfiguration config
    )
    {
        this.userManager = userManager;
        this.dbContext = dbContext;

        chaveAssinaturaJwt = config["JWT_GENERATION_KEY"]
            ?? throw new ArgumentException("Cifra de geração de tokens não configurada.");

        audienciaValida = config["JWT_AUDIENCE_DOMAIN"]
            ?? throw new ArgumentException("Audiência válida não configurada.");
    }

    public async Task<AccessToken> GerarAccessTokenAsync(Usuario usuario)
    {
        var roles = await userManager.GetRolesAsync(usuario);

        var cargoDoUsuarioStr = roles.FirstOrDefault(); // Empresa / Funcionario

        if (cargoDoUsuarioStr is null)
            throw new Exception("Não foi possível recuperar os dados de permissão do usuário.");

        Guid empresaId = usuario.Id;

        //if (cargoDoUsuarioStr == CargoUsuario.Funcionario.ToString())
        //{
        //    // Se for funcionário, busca a empresa vinculada
        //    var funcionario = await dbContext.Set<Funcionario>()
        //        .AsNoTracking()
        //        .IgnoreQueryFilters()
        //        .FirstOrDefaultAsync(f => f.UsuarioId == usuario.Id && !f.Excluido);

        //    if (funcionario is null)
        //        throw new Exception("Funcionário não encontrado ou inativo.");

        //    empresaId = funcionario.EmpresaId;
        //}
        //else
        //{
        //    empresaId = usuario.Id;
        //}

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, usuario.UserName!),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, usuario.AccessTokenVersionId.ToString()),
            new Claim(ClaimTypes.Role, cargoDoUsuarioStr),
            new Claim("EmpresaId", empresaId.ToString())
        };

        var expiracaoJwt = DateTime.UtcNow.AddMinutes(15);

        var chaveEmBytes = Encoding.ASCII.GetBytes(chaveAssinaturaJwt);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = "LocadoraDeVeiculos",
            Audience = audienciaValida,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(chaveEmBytes),
                SecurityAlgorithms.HmacSha256Signature
            ),
            Expires = expiracaoJwt
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return new AccessToken(
            tokenString,
            expiracaoJwt,
             new UsuarioAutenticado(
                usuario.Id,
                usuario.FullName,
                usuario.Email ?? string.Empty,
                Enum.Parse<CargoUsuario>(cargoDoUsuarioStr)
            )
        );
    }
}