using FluentResults;
using MediatR;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;

// UsuarioId vem sempre da claim do JWT autenticado (nunca do corpo da requisição):
// ninguém pode cadastrar rosto em nome de outra pessoa.
public record CadastrarRostoCommand(System.Guid UsuarioId, string ImagemBase64)
    : IRequest<Result<int>>;
