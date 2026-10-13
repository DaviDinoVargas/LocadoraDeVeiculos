using FluentResults;
using MediatR;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;

/// <summary>Responde sempre "ok": quem pede não descobre se o e-mail tem conta.</summary>
public record SolicitarRecuperacaoDeSenhaCommand(string Email) : IRequest<Result>;

public record RedefinirSenhaCommand(string Email, string Token, string NovaSenha, string ConfirmarNovaSenha) : IRequest<Result>;
