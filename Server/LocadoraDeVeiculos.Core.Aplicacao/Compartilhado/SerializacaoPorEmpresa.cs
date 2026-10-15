using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using MediatR;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;

/// <summary>
/// Marca os comandos que mudam o estado de aluguéis e devoluções. Eles rodam um de cada vez POR EMPRESA: sem isso, duas requisições
/// simultâneas passam juntas pela checagem ("o carro está livre?", "o cupom ainda tem uso?", "o aluguel ainda não foi iniciado?")
/// e gravam as duas (reserva dupla, cupom de uso único usado várias vezes, transição repetida).
/// </summary>
public interface ISerializadoPorEmpresa;

/// <summary>
/// As travas ficam FORA do behavior de propósito: o behavior é genérico (um tipo fechado por comando) e um campo estático dentro de classe genérica
/// existe uma vez para cada combinação de tipos. Com a trava lá dentro, "cancelar" e "iniciar" o mesmo aluguel teriam travas diferentes e rodariam juntos.
/// Aqui existe um único semáforo por empresa, compartilhado por todos os comandos marcados.
/// </summary>
internal static class TravasPorEmpresa
{
    public static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Travas = new();
}

/// <summary>
/// Trava em memória por empresa ao redor dos comandos <see cref="ISerializadoPorEmpresa"/>. Vale para UMA instância da API; com várias
/// instâncias é preciso uma trava no banco (sp_getapplock) ou um token de concorrência na tabela.
/// </summary>
public sealed class SerializarPorEmpresaBehavior<TRequest, TResponse>(ITenantProvider tenant) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ISerializadoPorEmpresa || tenant.EmpresaId is not { } empresa)
            return await next(cancellationToken);

        var trava = TravasPorEmpresa.Travas.GetOrAdd(empresa, _ => new SemaphoreSlim(1, 1));

        await trava.WaitAsync(cancellationToken);

        try
        {
            return await next(cancellationToken);
        }
        finally
        {
            trava.Release();
        }
    }
}
