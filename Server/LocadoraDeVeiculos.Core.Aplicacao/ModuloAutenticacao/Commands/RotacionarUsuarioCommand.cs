using FluentResults;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;

public record RotacionarTokenCommand(string RefreshTokenString)
    : IRequest<Result<(AccessToken AccessToken, RefreshToken RefreshToken)>>;
