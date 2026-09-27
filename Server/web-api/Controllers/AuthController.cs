
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Config.Identify;
using LocadoraDeVeiculos.WebApi.Models.ModuloAutenticacao;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LocadoraDeVeiculos.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
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
        var refreshTokenBruto = RefreshTokenCookieService.Get(Request);

        if (refreshTokenBruto is null)
            return Unauthorized("O token de rotação não foi encontrado.");

        var result = await mediator.Send(new RotacionarTokenCommand(refreshTokenBruto));

        return ProcessarResultado(result, ResponderComToken);
    }

    [HttpPost("sair")]
    public async Task<IActionResult> Sair()
    {
        var refreshTokenBruto = RefreshTokenCookieService.Get(Request);

        if (refreshTokenBruto is null)
            return Unauthorized("O token de rotação não foi encontrado.");

        var result = await mediator.Send(new SairCommand(refreshTokenBruto));

        return ProcessarResultado(result, () =>
        {
            RefreshTokenCookieService.LimparCookie(Response);

            return NoContent();
        });
    }

    private ActionResult ResponderComToken((AccessToken AccessToken, RefreshToken RefreshToken, string RefreshTokenBruto) valor)
    {
        RefreshTokenCookieService.EnviarCookie(Response, valor.RefreshTokenBruto, valor.RefreshToken.ExpiraEmUtc);

        return Ok(valor.AccessToken);
    }
}