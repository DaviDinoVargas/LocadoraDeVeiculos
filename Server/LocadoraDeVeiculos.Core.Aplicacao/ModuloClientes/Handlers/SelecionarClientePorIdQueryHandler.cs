using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCliente.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCliente.Handlers
{
    public class SelecionarClientePorIdQueryHandler
        : IRequestHandler<SelecionarClientePorIdQuery, Result<SelecionarClientePorIdResult>>
    {
        private readonly IRepositorioCliente _repositorioCliente;

        public SelecionarClientePorIdQueryHandler(IRepositorioCliente repositorioCliente)
        {
            _repositorioCliente = repositorioCliente;
        }

        public async Task<Result<SelecionarClientePorIdResult>> Handle(
            SelecionarClientePorIdQuery query, CancellationToken cancellationToken)
        {
            var cliente = await _repositorioCliente.SelecionarPorIdAsync(query.Id);

            if (cliente is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(query.Id));

            return cliente switch
            {
                ClientePessoaFisica pf => HandlePessoaFisica(pf),
                ClientePessoaJuridica pj => HandlePessoaJuridica(pj),
                _ => Result.Fail<SelecionarClientePorIdResult>("Tipo de cliente desconhecido")
            };
        }

        private Result<SelecionarClientePorIdResult> HandlePessoaFisica(ClientePessoaFisica pf)
        {
            var result = new SelecionarClientePorIdResult(
                pf.Id,
                pf.Nome,
                pf.Telefone,
                pf.Email,
                pf.Endereco,
                "PessoaFisica",
                pf.Cpf,
                pf.Rg,
                pf.Cnh,
                pf.ValidadeCnh,
                pf.ClientePessoaJuridicaId,
                pf.ClientePessoaJuridica?.Nome,
                null,
                null
            );

            return Result.Ok(result);
        }

        private Result<SelecionarClientePorIdResult> HandlePessoaJuridica(ClientePessoaJuridica pj)
        {
            var result = new SelecionarClientePorIdResult(
                pj.Id,
                pj.Nome,
                pj.Telefone,
                pj.Email,
                pj.Endereco,
                "PessoaJuridica",
                null,
                null,
                null,
                null,
                null,
                null,
                pj.Cnpj,
                pj.NomeFantasia
            );

            return Result.Ok(result);
        }
    }
}