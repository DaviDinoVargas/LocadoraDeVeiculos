using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;

/// <summary>
/// Trava de aplicação do SQL Server (sp_getapplock) por empresa: vale entre INSTÂNCIAS da API, ao contrário do semáforo em memória.
/// Com duas instâncias no mesmo banco, o semáforo de cada uma só enxerga os pedidos dela e as duas passavam juntas pela checagem "o carro está
/// livre?" (reserva dupla em 12 de 12 rodadas do ataque N01). A trava fica na conexão do contexto da requisição, que é a mesma que o handler usa,
/// e é solta (e a conexão fechada) no fim, com sucesso ou erro. Em provedores sem esse recurso (testes em memória) não faz nada.
/// </summary>
internal sealed class TravaNoBanco : IAsyncDisposable
{
    private const string ProvedorSqlServer = "Microsoft.EntityFrameworkCore.SqlServer";
    private const int EsperaMaximaEmMilissegundos = 30_000;

    private readonly DbContext _banco;
    private readonly string _recurso;

    private TravaNoBanco(DbContext banco, string recurso)
    {
        _banco = banco;
        _recurso = recurso;
    }

    /// <summary>Nome do recurso travado; uma empresa, uma trava.</summary>
    internal static string RecursoDaEmpresa(Guid empresa) => $"locadora:serializar:{empresa:N}";

    public static async Task<TravaNoBanco?> AdquirirAsync(DbContext? banco, Guid empresa, CancellationToken cancellationToken)
    {
        // sem transação aberta: o comando da trava não pode ser anexado a uma transação que outro código controla
        if (banco is null || banco.Database.ProviderName != ProvedorSqlServer || banco.Database.CurrentTransaction is not null)
            return null;

        var recurso = RecursoDaEmpresa(empresa);

        // EF mantém a conexão aberta até o CloseConnection correspondente: o handler, que usa este mesmo contexto, roda dentro da trava
        await banco.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            var resultado = await ExecutarAsync(banco, """
                DECLARE @r int;
                EXEC @r = sp_getapplock @Resource = @recurso, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = @espera;
                SELECT @r;
                """, recurso, cancellationToken);

            // 0/1 = obtida; negativo = não obteve (tempo esgotado, cancelada, deadlock)
            if (resultado < 0)
                throw new TimeoutException("Não foi possível obter a trava da empresa no banco a tempo (outra operação da mesma empresa está demorando).");

            return new TravaNoBanco(banco, recurso);
        }
        catch
        {
            await banco.Database.CloseConnectionAsync();
            throw;
        }
    }

    private static async Task<int> ExecutarAsync(DbContext banco, string sql, string recurso, CancellationToken cancellationToken)
    {
        var comando = banco.Database.GetDbConnection().CreateCommand();
        await using (comando.ConfigureAwait(false))
        {
            comando.CommandText = sql;
            comando.CommandType = CommandType.Text;

            var parametroRecurso = comando.CreateParameter();
            parametroRecurso.ParameterName = "@recurso";
            parametroRecurso.Value = recurso;
            comando.Parameters.Add(parametroRecurso);

            var parametroEspera = comando.CreateParameter();
            parametroEspera.ParameterName = "@espera";
            parametroEspera.Value = EsperaMaximaEmMilissegundos;
            comando.Parameters.Add(parametroEspera);

            return Convert.ToInt32(await comando.ExecuteScalarAsync(cancellationToken));
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            var comando = _banco.Database.GetDbConnection().CreateCommand();
            await using (comando.ConfigureAwait(false))
            {
                comando.CommandText = "EXEC sp_releaseapplock @Resource = @recurso, @LockOwner = 'Session';";
                var parametro = comando.CreateParameter();
                parametro.ParameterName = "@recurso";
                parametro.Value = _recurso;
                comando.Parameters.Add(parametro);
                await comando.ExecuteNonQueryAsync(CancellationToken.None);
            }
        }
        catch
        {
            // se a conexão caiu, o SQL Server solta a trava quando a sessão acaba; o importante é não esconder o resultado do comando
        }
        finally
        {
            await _banco.Database.CloseConnectionAsync();
        }
    }
}
