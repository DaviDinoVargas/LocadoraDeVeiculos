using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands.Autenticar;
public record AutenticarUsuarioRequest(string UserName, string Password) : IRequest<Result<TokenResponse>>;