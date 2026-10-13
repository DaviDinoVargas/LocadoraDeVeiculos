using FluentResults;
using MediatR;
using System;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;

// UsuarioId vem sempre da claim do JWT. A senha é exigida de novo: um token roubado não encerra a conta.
public record EncerrarContaCommand(Guid UsuarioId, string Senha) : IRequest<Result>;
