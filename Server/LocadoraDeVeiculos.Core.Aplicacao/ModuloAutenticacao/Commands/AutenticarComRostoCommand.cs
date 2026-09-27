using FluentResults;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using MediatR;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;

public record AutenticarComRostoCommand(string Email, string ImagemBase64)
    : IRequest<Result<(AccessToken AccessToken, RefreshToken RefreshToken, string RefreshTokenBruto)>>;
