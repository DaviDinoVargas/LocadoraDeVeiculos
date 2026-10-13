using FluentResults;
using MediatR;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;

// UsuarioId vem sempre da claim do JWT autenticado (nunca do corpo da requisição):
// ninguém pode cadastrar rosto em nome de outra pessoa.
// Consentimento e VersaoDoTexto: sem o aceite explícito da versão ATUAL do termo (ConsentimentoBiometrico) não há cadastro.
public record CadastrarRostoCommand(System.Guid UsuarioId, string ImagemBase64, bool Consentimento = false, string? VersaoDoTexto = null)
    : IRequest<Result<int>>;
