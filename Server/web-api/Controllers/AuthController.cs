
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Config.Http;
using LocadoraDeVeiculos.WebApi.Config.Identify;
using LocadoraDeVeiculos.WebApi.Filters;
using LocadoraDeVeiculos.WebApi.Models.ModuloAutenticacao;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace LocadoraDeVeiculos.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AutenticacaoController(IMediator mediator) : MainController
{
    // Cadastro aberto (S6): limite próprio e bem mais apertado que o do login.
    [EnableRateLimiting(SegurancaHttpConfig.PoliticaRegistro)]
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

    [HttpPost("entrar-facial")]
    public async Task<ActionResult<AccessToken>> EntrarFacial(AutenticarComRostoRequest request)
    {
        var command = new AutenticarComRostoCommand(request.Email, request.ImagemBase64);

        var result = await mediator.Send(command);

        return ProcessarResultado(result, ResponderComToken);
    }

    [HttpPost("rosto")]
    [Authorize]
    public async Task<ActionResult<CadastrarRostoResponse>> CadastrarRosto(CadastrarRostoRequest request)
    {
        if (!TentarObterUsuarioId(out var usuarioId))
            return Unauthorized();

        var command = new CadastrarRostoCommand(usuarioId, request.ImagemBase64);

        var result = await mediator.Send(command);

        return ProcessarResultado(result, amostras => Ok(new CadastrarRostoResponse(amostras)));
    }

    // Consulta e remoção exigem login e só enxergam o próprio rosto: não são alvo de força bruta,
    // então ficam fora do limite de 10 req/min do "auth" (a tela consulta a cada visita).
    [HttpGet("rosto")]
    [Authorize]
    [DisableRateLimiting]
    public async Task<ActionResult<StatusRostoResponse>> ObterStatusRosto(CancellationToken cancellationToken)
    {
        if (!TentarObterUsuarioId(out var usuarioId))
            return Unauthorized();

        var result = await mediator.Send(new ObterStatusRostoQuery(usuarioId), cancellationToken);

        return ProcessarResultado(result, cadastrado => Ok(new StatusRostoResponse(cadastrado)));
    }

    [HttpPost("rosto/verificar")]
    [Authorize]
    public async Task<ActionResult<VerificarRostoResponse>> VerificarRosto(VerificarRostoRequest request, CancellationToken cancellationToken)
    {
        if (!TentarObterUsuarioId(out var usuarioId))
            return Unauthorized();

        var result = await mediator.Send(new VerificarRostoCommand(usuarioId, request.ImagemBase64), cancellationToken);

        return ProcessarResultado(result, valor => Ok(new VerificarRostoResponse(valor.Confirmado, valor.Confianca)));
    }

    // Apaga o rosto do PRÓPRIO usuário logado (direito de remover dado biométrico).
    // Depois disso o login facial deixa de funcionar para ele até cadastrar de novo.
    [HttpDelete("rosto")]
    [Authorize]
    [DisableRateLimiting]
    public async Task<IActionResult> RemoverRosto(CancellationToken cancellationToken)
    {
        if (!TentarObterUsuarioId(out var usuarioId))
            return Unauthorized();

        var result = await mediator.Send(new RemoverRostoCommand(usuarioId), cancellationToken);

        return ProcessarResultado(result);
    }

    // Usado pelo serviço de ML para validar o JWT que o Angular envia às câmeras: ele não conhece
    // a chave de assinatura, então pergunta aqui. Fora do rate limit do "auth" porque o ML consulta
    // (com cache) a cada requisição do monitoramento.
    [HttpGet("sessao")]
    [Authorize]
    [DisableRateLimiting]
    public IActionResult ObterSessao() => NoContent();

    [HttpPost("rotacionar")]
    [ExigirCabecalhoCliente]
    public async Task<ActionResult<AccessToken>> Rotacionar()
    {
        var refreshTokenBruto = RefreshTokenCookieService.Get(Request);

        if (refreshTokenBruto is null)
            return Unauthorized("O token de rotação não foi encontrado.");

        var result = await mediator.Send(new RotacionarTokenCommand(refreshTokenBruto));

        return ProcessarResultado(result, ResponderComToken);
    }

    [HttpPost("sair")]
    [ExigirCabecalhoCliente]
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

    private bool TentarObterUsuarioId(out Guid usuarioId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out usuarioId);

    private ActionResult ResponderComToken((AccessToken AccessToken, RefreshToken RefreshToken, string RefreshTokenBruto) valor)
    {
        RefreshTokenCookieService.EnviarCookie(Response, valor.RefreshTokenBruto, valor.RefreshToken.ExpiraEmUtc);

        return Ok(valor.AccessToken);
    }
}