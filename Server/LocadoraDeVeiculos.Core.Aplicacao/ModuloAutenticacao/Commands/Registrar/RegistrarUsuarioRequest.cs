using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands.Registrar;

public record RegistrarUsuarioRequest(string UserName, string Email, string Password)
    : IRequest<Result<TokenResponse>>;