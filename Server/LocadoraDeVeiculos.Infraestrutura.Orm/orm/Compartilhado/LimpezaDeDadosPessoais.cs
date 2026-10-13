using LocadoraDeVeiculos.Core.Dominio.ModuloAuditoria;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;

/// <summary>Resultado de uma rodada da limpeza, para o log (só contagens, nunca dados).</summary>
public record ResultadoDaLimpeza(int AmostrasFaciais, int Consentimentos, int EnderecosIpAnonimizados, int RefreshTokens);

/// <summary>
/// Política de retenção de dados pessoais (S1/S14/S29). Roda periodicamente (hospedada na Web API) e também pode ser
/// chamada à mão:
/// - rosto: apagado 12 meses depois do último uso (o consentimento segue junto);
/// - IP da trilha de auditoria: anonimizado depois de 180 dias (o evento continua, o endereço não);
/// - refresh tokens (IP e User-Agent): apagados 30 dias depois de vencidos ou revogados.
/// </summary>
public class LimpezaDeDadosPessoais(LocadoraDeVeiculosDbContext dbContext)
{
    public static readonly TimeSpan RetencaoDoRostoSemUso = TimeSpan.FromDays(365);
    public static readonly TimeSpan RetencaoDoIpNaAuditoria = TimeSpan.FromDays(180);
    public static readonly TimeSpan RetencaoDeTokensEncerrados = TimeSpan.FromDays(30);

    public async Task<ResultadoDaLimpeza> ExecutarAsync(DateTime agoraUtc, CancellationToken cancellationToken)
    {
        // --- rosto sem uso há 12 meses (e consentimentos que ficaram sem rosto)
        var limiteRosto = agoraUtc - RetencaoDoRostoSemUso;
        var amostras = await dbContext.Set<AmostraFacial>().Where(a => a.UltimoUsoEmUtc < limiteRosto).ToListAsync(cancellationToken);
        var pessoasVencidas = amostras.Select(a => a.PessoaId).ToHashSet();
        dbContext.Set<AmostraFacial>().RemoveRange(amostras);

        // Consentimento sem rosto não tem mais finalidade: some junto com o rosto (ou se nunca houve rosto).
        var consentimentos = await dbContext.Set<ConsentimentoFacial>().ToListAsync(cancellationToken);
        var comRosto = (await dbContext.Set<AmostraFacial>().Select(a => a.PessoaId).Distinct().ToListAsync(cancellationToken)).ToHashSet();
        comRosto.ExceptWith(pessoasVencidas);
        var consentimentosSemRosto = consentimentos.Where(c => !comRosto.Contains(c.PessoaId)).ToList();
        dbContext.Set<ConsentimentoFacial>().RemoveRange(consentimentosSemRosto);

        // --- IP da auditoria
        var limiteIp = agoraUtc - RetencaoDoIpNaAuditoria;
        var comIp = await dbContext.Set<RegistroDeAuditoria>()
            .IgnoreQueryFilters()
            .Where(r => r.EnderecoIp != null && r.EmUtc < limiteIp)
            .ToListAsync(cancellationToken);

        foreach (var registro in comIp)
            registro.EnderecoIp = null;

        // --- refresh tokens encerrados
        var limiteToken = agoraUtc - RetencaoDeTokensEncerrados;
        var tokens = await dbContext.RefreshTokens
            .Where(t => t.ExpiraEmUtc < limiteToken || (t.RevogadoEmUtc != null && t.RevogadoEmUtc < limiteToken))
            .ToListAsync(cancellationToken);
        dbContext.RefreshTokens.RemoveRange(tokens);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResultadoDaLimpeza(amostras.Count, consentimentosSemRosto.Count, comIp.Count, tokens.Count);
    }
}
