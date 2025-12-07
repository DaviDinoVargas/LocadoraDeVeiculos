using Azure.Core;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands.Autenticar;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands.Registrar;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Config.Identify;
using LocadoraDeVeiculos.WebApi.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LocadoraDeVeiculos.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AutenticacaoController(IMediator mediator) : MainController
{
    [HttpPost("registrar")]
    public async Task<ActionResult<AccessToken>> Registrar(RegistrarUsuarioRequest request)
    {
        var command = new RegistrarUsuarioCommand(
            request.NomeCompleto,
            request.Email,
            request.Senha,
            request.ConfirmarSenha
        );

        var result = await mediator.Send(command);

        return ProcessarResultado(result, ResponderComToken);
    }

    [HttpPost("autenticar")]
    public async Task<ActionResult<AccessToken>> Autenticar(AutenticarUsuarioRequest request)
    {
        var command = new AutenticarUsuarioCommand(request.Email, request.Senha);

        var result = await mediator.Send(command);

        return ProcessarResultado(result, ResponderComToken);
    }

    [HttpPost("rotacionar")]
    public async Task<ActionResult<AccessToken>> Rotacionar()
    {
        var refreshToken = RefreshTokenCookieService.Get(Request);

        if (refreshToken is null)
            return Unauthorized("O token de rotação não foi encontrado.");

        var result = await mediator.Send(new RotacionarTokenCommand(refreshToken));

        return ProcessarResultado(result, ResponderComToken);
    }

    [HttpPost("sair")]
    public async Task<IActionResult> Sair()
    {
        var refreshTokenHash = RefreshTokenCookieService.Get(Request);

        if (refreshTokenHash is null)
            return Unauthorized("O token de rotação não foi encontrado.");

        var result = await mediator.Send(new SairCommand(refreshTokenHash));

        return ProcessarResultado(result, () =>
        {
            RefreshTokenCookieService.LimparCookie(Response);

            return NoContent();
        });
    }

    private ActionResult ResponderComToken((AccessToken AccessToken, RefreshToken RefreshToken) valor)
    {
        RefreshTokenCookieService.EnviarCookie(Response, valor.RefreshToken);

        return Ok(valor.AccessToken);
    }
}