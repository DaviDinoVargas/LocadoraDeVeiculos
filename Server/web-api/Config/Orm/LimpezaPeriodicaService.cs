using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;

namespace LocadoraDeVeiculos.WebApi.Config.Orm;

/// <summary>
/// Roda a política de retenção de dados pessoais (rosto sem uso há 12 meses, IP antigo da auditoria, refresh tokens vencidos)
/// uma vez por dia e uma vez logo depois da subida. Falha numa rodada vira aviso no log e a próxima tenta de novo.
/// </summary>
public sealed class LimpezaPeriodicaService(IServiceScopeFactory escopos, ILogger<LimpezaPeriodicaService> logger) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);
    private static readonly TimeSpan AtrasoInicial = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken parada)
    {
        try
        {
            await Task.Delay(AtrasoInicial, parada);

            while (!parada.IsCancellationRequested)
            {
                await ExecutarUmaVezAsync(parada);
                await Task.Delay(Intervalo, parada);
            }
        }
        catch (OperationCanceledException)
        {
            // desligamento normal
        }
    }

    public async Task ExecutarUmaVezAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var escopo = escopos.CreateScope();
            var limpeza = escopo.ServiceProvider.GetRequiredService<LimpezaDeDadosPessoais>();

            var resultado = await limpeza.ExecutarAsync(DateTime.UtcNow, cancellationToken);

            logger.LogInformation(
                "Limpeza de dados pessoais: {Amostras} amostra(s) facial(is), {Consentimentos} consentimento(s), {Ips} IP(s) anonimizado(s), {Tokens} refresh token(s).",
                resultado.AmostrasFaciais, resultado.Consentimentos, resultado.EnderecosIpAnonimizados, resultado.RefreshTokens);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "A limpeza de dados pessoais falhou; será tentada de novo na próxima rodada.");
        }
    }
}
