
using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.DTOs;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using MediatR;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands.Autenticar;

public class AutenticarUsuarioRequestHandler(
    SignInManager<Usuario> signInManager,
    UserManager<Usuario> userManager,
    ITokenProvider tokenProvider
) : IRequestHandler<AutenticarUsuarioRequest, Result<TokenResponse>>
{
    public async Task<Result<TokenResponse>> Handle(
        AutenticarUsuarioRequest request, CancellationToken cancellationToken)
    {
        var loginResult = await signInManager.PasswordSignInAsync(
            request.UserName,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true
        );

        var usuario = await userManager.FindByNameAsync(request.UserName);

        if (usuario == null)
        {
            return Result.Fail(
                AuthErrorResults.UsuarioNaoEncontradoError(request.UserName));
        }

        if (loginResult.IsLockedOut)
            return Result.Fail(AuthErrorResults.UsuarioBloqueadoError());

        if (loginResult.IsNotAllowed)
        {
            if (!await userManager.IsEmailConfirmedAsync(usuario) && usuario.Email != null)
                return Result.Fail(AuthErrorResults.ConfirmacaoEmailPendenteError(usuario.Email));
        }

        if (!loginResult.Succeeded)
            return Result.Fail(AuthErrorResults.CredenciaisIncorretasError());

        var tokenAcesso = tokenProvider.GerarTokenDeAcesso(usuario) as TokenResponse;

        if (tokenAcesso == null)
            return Result.Fail(ErrorResults.InternalServerError(new Exception("Falha ao gerar token de acesso")));

        return Result.Ok(tokenAcesso);
    }
}