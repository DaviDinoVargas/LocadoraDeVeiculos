using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;

namespace LocadoraDeVeiculos.WebApi.Config.Identify;

/// <summary>Consome a fila de "esqueci minha senha" fora da requisição (S18). Uma falha num pedido não derruba os demais.</summary>
public sealed class ProcessadorDeRecuperacaoDeSenha(
    FilaDeRecuperacaoDeSenha fila,
    IServiceScopeFactory escopos,
    ILogger<ProcessadorDeRecuperacaoDeSenha> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken parada)
    {
        try
        {
            await foreach (var email in fila.Leitor.ReadAllAsync(parada))
            {
                try
                {
                    using var escopo = escopos.CreateScope();
                    await escopo.ServiceProvider.GetRequiredService<RecuperacaoDeSenha>().ProcessarPedidoAsync(email, parada);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Falha ao processar um pedido de recuperação de senha.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // desligamento normal
        }
    }
}
