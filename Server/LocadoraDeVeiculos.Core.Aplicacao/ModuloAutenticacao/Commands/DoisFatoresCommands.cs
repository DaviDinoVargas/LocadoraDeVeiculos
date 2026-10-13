using FluentResults;
using MediatR;
using System;
using System.Collections.Generic;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;

// Em todos, UsuarioId vem da claim do JWT (nunca do corpo): cada pessoa só mexe no próprio 2FA.
public record ObterStatusDoisFatoresQuery(Guid UsuarioId) : IRequest<Result<StatusDoisFatores>>;

public record StatusDoisFatores(bool Habilitado, int CodigosDeRecuperacaoRestantes);

public record IniciarDoisFatoresCommand(Guid UsuarioId) : IRequest<Result<ConfiguracaoDoisFatores>>;

/// <summary>Chave para o aplicativo autenticador: "Chave" para digitar, "Uri" (otpauth://) para gerar o QR code.</summary>
public record ConfiguracaoDoisFatores(string Chave, string Uri);

public record AtivarDoisFatoresCommand(Guid UsuarioId, string Codigo) : IRequest<Result<IReadOnlyList<string>>>;

public record DesativarDoisFatoresCommand(Guid UsuarioId, string Senha, string Codigo) : IRequest<Result>;
