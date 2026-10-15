using System;

namespace LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;

/// <summary>
/// Reconhece o erro que o SQL Server devolve quando duas requisições simultâneas passam juntas pela checagem "já existe?" e a segunda esbarra
/// no índice único (2601 = índice único, 2627 = restrição UNIQUE). Esse erro é uma resposta esperada de concorrência — "alguém acabou de cadastrar
/// o mesmo registro" — e deve virar 409, não "erro interno do servidor". O banco continua sendo a defesa final contra o duplicado; aqui só se
/// traduz a resposta.
/// </summary>
public static class ViolacaoDeUnicidade
{
    private static readonly int[] CodigosDoSqlServer = [2601, 2627];

    public static bool Eh(Exception? excecao)
    {
        for (var atual = excecao; atual is not null; atual = atual.InnerException)
        {
            // Reconhecido pelo nome e pelo número do erro (sem depender do pacote do driver): SqlException.Number
            if (atual.GetType().Name == "SqlException"
                && atual.GetType().GetProperty("Number")?.GetValue(atual) is int numero
                && CodigosDoSqlServer.Contains(numero))
                return true;
        }

        return false;
    }
}
