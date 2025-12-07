using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;

public class RegistrarUsuarioCommandHandler(
    UserManager<Usuario> userManager,
    RoleManager<Cargo> roleManager,
    AccessTokenProvider tokenProvider,
    RefreshTokenProvider refreshTokenProvider,
    ILogger<RegistrarUsuarioCommandHandler> logger
) : IRequestHandler<RegistrarUsuarioCommand, Result<(AccessToken, RefreshToken)>>
{
    public async Task<Result<(AccessToken, RefreshToken)>> Handle(
        RegistrarUsuarioCommand command, CancellationToken cancellationToken)
    {
        Usuario? usuario = null;

        try
        {
            if (!command.Senha.Equals(command.ConfirmarSenha))
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro("A confirmação de senha falhou."));

            usuario = new Usuario
            {
                FullName = command.NomeCompleto,
                UserName = command.Email,
                Email = command.Email
            };

            var usuarioResult = await userManager.CreateAsync(usuario, command.Senha);

            if (!usuarioResult.Succeeded)
            {
                var erros = usuarioResult.Errors.Select(err =>
                {
                    return err.Code switch
                    {
                        "DuplicateUserName" => "Já existe um usuário com esse nome.",
                        "DuplicateEmail" => "Já existe um usuário com esse e-mail.",
                        "PasswordTooShort" => "A senha é muito curta.",
                        "PasswordRequiresNonAlphanumeric" => "A senha deve conter pelo menos um caractere especial.",
                        "PasswordRequiresDigit" => "A senha deve conter pelo menos um número.",
                        "PasswordRequiresUpper" => "A senha deve conter pelo menos uma letra maiúscula.",
                        "PasswordRequiresLower" => "A senha deve conter pelo menos uma letra minúscula.",
                        _ => err.Description
                    };
                });

                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            var cargoStr = CargoUsuario.Empresa.ToString();

            var resultadoBuscaCargo = await roleManager.FindByNameAsync(cargoStr);

            if (resultadoBuscaCargo is null)
            {
                var cargo = new Cargo()
                {
                    Name = cargoStr,
                    NormalizedName = cargoStr.ToUpperInvariant(),
                    ConcurrencyStamp = Guid.NewGuid().ToString()
                };

                await roleManager.CreateAsync(cargo);
            }

            var resultadoInclusaoCargo = await userManager.AddToRoleAsync(usuario, cargoStr);

            if (!resultadoInclusaoCargo.Succeeded)
            {
                var erros = resultadoInclusaoCargo.Errors.Select(e => e.Description);

                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            var accessToken = await tokenProvider.GerarAccessTokenAsync(usuario);

            if (accessToken is null)
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(new Exception("Falha ao gerar token de acesso.")));

            var refreshToken = await refreshTokenProvider.GerarRefreshTokenAsync(usuario);

            if (refreshToken is null)
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(new Exception("Falha ao gerar token de rotação.")));

            return Result.Ok((accessToken, refreshToken));
        }
        catch (Exception ex)
        {
            if (usuario is not null)
                await userManager.DeleteAsync(usuario);

            logger.LogError(
                ex,
                "Ocorreu um erro durante o registro de {@Command}.",
                command
            );

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}
