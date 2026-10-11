using FluentResults;
using MediatR;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;

// Em todos, UsuarioId vem da claim do JWT autenticado (nunca do corpo/rota): cada pessoa só
// consulta, confere ou apaga o PRÓPRIO rosto. O personId enviado ao serviço de ML é derivado
// do e-mail do usuário no servidor, o navegador não escolhe.
public record ObterStatusRostoQuery(System.Guid UsuarioId) : IRequest<Result<bool>>;

public record VerificarRostoCommand(System.Guid UsuarioId, string ImagemBase64)
    : IRequest<Result<VerificacaoRostoResultado>>;

public record VerificacaoRostoResultado(bool Confirmado, double Confianca);

public record RemoverRostoCommand(System.Guid UsuarioId) : IRequest<Result>;
