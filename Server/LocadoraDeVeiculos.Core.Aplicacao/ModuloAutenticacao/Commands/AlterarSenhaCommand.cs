using FluentResults;
using MediatR;
using System;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;

public record AlterarSenhaCommand(
    Guid UsuarioId,
    string SenhaAtual,
    string NovaSenha,
    string ConfirmarNovaSenha
) : IRequest<Result>;
