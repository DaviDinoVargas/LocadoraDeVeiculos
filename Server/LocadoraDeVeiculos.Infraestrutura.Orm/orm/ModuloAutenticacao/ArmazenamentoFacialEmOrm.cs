using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutenticacao;

public class ArmazenamentoFacialEmOrm(LocadoraDeVeiculosDbContext dbContext, CifradorFacial cifrador) : IArmazenamentoFacial
{
    // Amostras recentes de ângulos levemente diferentes melhoram o acerto; mais que isso só acumula biometria.
    public const int MaximoDeAmostras = 3;

    public async Task<int> SalvarAsync(string pessoaId, IReadOnlyList<double> landmarks, CancellationToken cancellationToken)
    {
        var pessoa = Normalizar(pessoaId);

        dbContext.Set<AmostraFacial>().Add(new AmostraFacial
        {
            PessoaId = pessoa,
            Conteudo = cifrador.Cifrar(JsonSerializer.SerializeToUtf8Bytes(landmarks), pessoa)
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        // Mantém só as mais recentes; as demais são apagadas de fato.
        var antigas = (await dbContext.Set<AmostraFacial>()
                .Where(a => a.PessoaId == pessoa)
                .ToListAsync(cancellationToken))
            .OrderByDescending(a => a.CriadoEmUtc)
            .Skip(MaximoDeAmostras)
            .ToList();

        if (antigas.Count > 0)
        {
            dbContext.Set<AmostraFacial>().RemoveRange(antigas);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await dbContext.Set<AmostraFacial>().CountAsync(a => a.PessoaId == pessoa, cancellationToken);
    }

    public async Task<IReadOnlyList<IReadOnlyList<double>>> ObterAsync(string pessoaId, CancellationToken cancellationToken)
    {
        var pessoa = Normalizar(pessoaId);

        var amostras = await dbContext.Set<AmostraFacial>()
            .AsNoTracking()
            .Where(a => a.PessoaId == pessoa)
            .ToListAsync(cancellationToken);

        return amostras
            .OrderBy(a => a.CriadoEmUtc)
            .Select(a => (IReadOnlyList<double>)(JsonSerializer.Deserialize<double[]>(cifrador.Decifrar(a.Conteudo, pessoa)) ?? []))
            .ToList();
    }

    public Task<bool> ExisteAsync(string pessoaId, CancellationToken cancellationToken)
    {
        var pessoa = Normalizar(pessoaId);

        return dbContext.Set<AmostraFacial>().AnyAsync(a => a.PessoaId == pessoa, cancellationToken);
    }

    public async Task<bool> RemoverAsync(string pessoaId, CancellationToken cancellationToken)
    {
        var pessoa = Normalizar(pessoaId);

        var amostras = await dbContext.Set<AmostraFacial>().Where(a => a.PessoaId == pessoa).ToListAsync(cancellationToken);
        var consentimentos = await dbContext.Set<ConsentimentoFacial>().Where(c => c.PessoaId == pessoa).ToListAsync(cancellationToken);

        if (amostras.Count == 0 && consentimentos.Count == 0)
            return false;

        // Apagar o rosto é revogar o consentimento: os dois somem juntos.
        dbContext.Set<AmostraFacial>().RemoveRange(amostras);
        dbContext.Set<ConsentimentoFacial>().RemoveRange(consentimentos);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task RegistrarConsentimentoAsync(string pessoaId, string versaoDoTexto, CancellationToken cancellationToken)
    {
        var pessoa = Normalizar(pessoaId);

        var existente = await dbContext.Set<ConsentimentoFacial>().FirstOrDefaultAsync(c => c.PessoaId == pessoa, cancellationToken);

        if (existente is null)
            dbContext.Set<ConsentimentoFacial>().Add(new ConsentimentoFacial { PessoaId = pessoa, VersaoDoTexto = versaoDoTexto });
        else
        {
            existente.VersaoDoTexto = versaoDoTexto;
            existente.ConsentidoEmUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> ObterVersaoDoConsentimentoAsync(string pessoaId, CancellationToken cancellationToken)
    {
        var pessoa = Normalizar(pessoaId);

        return await dbContext.Set<ConsentimentoFacial>()
            .AsNoTracking()
            .Where(c => c.PessoaId == pessoa)
            .Select(c => c.VersaoDoTexto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task RegistrarUsoAsync(string pessoaId, CancellationToken cancellationToken)
    {
        var pessoa = Normalizar(pessoaId);

        var amostras = await dbContext.Set<AmostraFacial>().Where(a => a.PessoaId == pessoa).ToListAsync(cancellationToken);

        if (amostras.Count == 0)
            return;

        var agora = DateTime.UtcNow;

        foreach (var amostra in amostras)
            amostra.UltimoUsoEmUtc = agora;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string Normalizar(string pessoaId)
    {
        if (string.IsNullOrWhiteSpace(pessoaId))
            throw new ArgumentException("O identificador da pessoa é obrigatório.", nameof(pessoaId));

        return pessoaId.Trim().ToLowerInvariant();
    }
}
