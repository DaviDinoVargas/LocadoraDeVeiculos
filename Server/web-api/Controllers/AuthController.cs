
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
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
        var command = new AutenticarUsuarioCommand(request.Email, request.Senha, request.CodigoAutenticador);

        var result = await mediator.Send(command);

        return ProcessarResultado(result, ResponderComToken);
    }

    [HttpPost("entrar-facial")]
    public async Task<ActionResult<AccessToken>> EntrarFacial(AutenticarComRostoRequest request)
    {
        var command = new AutenticarComRostoCommand(request.Email, request.ImagemBase64, request.CodigoAutenticador);

        var result = await mediator.Send(command);

        return ProcessarResultado(result, ResponderComToken);
    }

    [HttpPost("rosto")]
    [Authorize]
    public async Task<ActionResult<CadastrarRostoResponse>> CadastrarRosto(CadastrarRostoRequest request)
    {
        if (!TentarObterUsuarioId(out var usuarioId))
            return Unauthorized();

        var command = new CadastrarRostoCommand(usuarioId, request.ImagemBase64, request.Consentimento, request.VersaoDoTexto);

        var result = await mediator.Send(command);

        return ProcessarResultado(result, amostras => Ok(new CadastrarRostoResponse(amostras)));
    }

    // Termo que o usuário precisa aceitar antes de cadastrar o rosto: o texto vem do servidor, a versão aceita volta no cadastro.
    [HttpGet("rosto/consentimento")]
    [Authorize]
    [DisableRateLimiting]
    public ActionResult<TermoDeConsentimentoResponse> ObterTermoDeConsentimento() =>
        Ok(new TermoDeConsentimentoResponse(
            ConsentimentoBiometrico.VersaoAtual, ConsentimentoBiometrico.Texto, ConsentimentoBiometrico.RetencaoEmMeses));

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

    // Troca de senha do próprio usuário (S27). Derruba todas as sessões: o cliente entra de novo com a senha nova.
    [HttpPost("alterar-senha")]
    [Authorize]
    public async Task<IActionResult> AlterarSenha(AlterarSenhaRequest request, CancellationToken cancellationToken)
    {
        if (!TentarObterUsuarioId(out var usuarioId))
            return Unauthorized();

        var result = await mediator.Send(
            new AlterarSenhaCommand(usuarioId, request.SenhaAtual, request.NovaSenha, request.ConfirmarNovaSenha), cancellationToken);

        return ProcessarResultado(result, () =>
        {
            RefreshTokenCookieService.LimparCookie(Response);

            return NoContent();
        });
    }

    // "Esqueci minha senha" (S18): resposta idêntica e imediata para qualquer e-mail; o envio acontece em segundo plano.
    [HttpPost("esqueci-senha")]
    public async Task<IActionResult> EsqueciSenha(SolicitarRecuperacaoRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new SolicitarRecuperacaoDeSenhaCommand(request.Email), cancellationToken);

        return Accepted(new { mensagem = "Se existir uma conta com este e-mail, enviaremos as instruções para redefinir a senha." });
    }

    [HttpPost("redefinir-senha")]
    public async Task<IActionResult> RedefinirSenha(RedefinirSenhaRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new RedefinirSenhaCommand(request.Email, request.Token, request.NovaSenha, request.ConfirmarNovaSenha), cancellationToken);

        return ProcessarResultado(result);
    }

    // Segundo fator por aplicativo autenticador (S18). Tudo exige login e só mexe no PRÓPRIO 2FA.
    [HttpGet("2fa")]
    [Authorize]
    [DisableRateLimiting]
    public async Task<ActionResult<StatusDoisFatoresResponse>> StatusDoisFatores(CancellationToken cancellationToken)
    {
        if (!TentarObterUsuarioId(out var usuarioId))
            return Unauthorized();

        var result = await mediator.Send(new ObterStatusDoisFatoresQuery(usuarioId), cancellationToken);

        return ProcessarResultado(result, v => Ok(new StatusDoisFatoresResponse(v.Habilitado, v.CodigosDeRecuperacaoRestantes)));
    }

    [HttpPost("2fa/iniciar")]
    [Authorize]
    public async Task<ActionResult<ConfiguracaoDoisFatoresResponse>> IniciarDoisFatores(CancellationToken cancellationToken)
    {
        if (!TentarObterUsuarioId(out var usuarioId))
            return Unauthorized();

        var result = await mediator.Send(new IniciarDoisFatoresCommand(usuarioId), cancellationToken);

        return ProcessarResultado(result, v => Ok(new ConfiguracaoDoisFatoresResponse(v.Chave, v.Uri)));
    }

    [HttpPost("2fa/ativar")]
    [Authorize]
    public async Task<ActionResult<CodigosDeRecuperacaoResponse>> AtivarDoisFatores(AtivarDoisFatoresRequest request, CancellationToken cancellationToken)
    {
        if (!TentarObterUsuarioId(out var usuarioId))
            return Unauthorized();

        var result = await mediator.Send(new AtivarDoisFatoresCommand(usuarioId, request.Codigo), cancellationToken);

        return ProcessarResultado(result, v => Ok(new CodigosDeRecuperacaoResponse(v)));
    }

    [HttpPost("2fa/desativar")]
    [Authorize]
    public async Task<IActionResult> DesativarDoisFatores(DesativarDoisFatoresRequest request, CancellationToken cancellationToken)
    {
        if (!TentarObterUsuarioId(out var usuarioId))
            return Unauthorized();

        var result = await mediator.Send(new DesativarDoisFatoresCommand(usuarioId, request.Senha, request.Codigo), cancellationToken);

        return ProcessarResultado(result);
    }

    // Encerra a conta da empresa e as dos funcionários e apaga a biometria de todos (S1). Só a Empresa, e com a senha de novo.
    [HttpPost("encerrar-conta")]
    [Authorize(Roles = "Empresa")]
    public async Task<IActionResult> EncerrarConta(EncerrarContaRequest request, CancellationToken cancellationToken)
    {
        if (!TentarObterUsuarioId(out var usuarioId))
            return Unauthorized();

        var result = await mediator.Send(new EncerrarContaCommand(usuarioId, request.Senha), cancellationToken);

        return ProcessarResultado(result, () =>
        {
            RefreshTokenCookieService.LimparCookie(Response);

            return NoContent();
        });
    }

    // Usado pelo serviço de ML para validar o JWT que o Angular envia às câmeras: ele não conhece
    // a chave de assinatura, então pergunta aqui. Fora do rate limit do "auth" porque o ML consulta
    // (com cache) a cada requisição do monitoramento.
    [HttpGet("sessao")]
    [Authorize]
    [DisableRateLimiting]
    public IActionResult ObterSessao()
    {
        // O ML precisa saber DE QUEM é a sessão (empresa e papel), não só se o token vale (S24).
        return Ok(new
        {
            usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            empresaId = User.FindFirstValue("EmpresaId"),
            cargo = User.FindFirstValue(ClaimTypes.Role)
        });
    }

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