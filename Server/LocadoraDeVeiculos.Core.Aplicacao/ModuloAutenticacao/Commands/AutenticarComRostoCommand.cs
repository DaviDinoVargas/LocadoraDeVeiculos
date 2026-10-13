using FluentResults;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using MediatR;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;

public record AutenticarComRostoCommand(string Email, string ImagemBase64, string? CodigoAutenticador = null)
    : IRequest<Result<(AccessToken AccessToken, RefreshToken RefreshToken, string RefreshTokenBruto)>>;
