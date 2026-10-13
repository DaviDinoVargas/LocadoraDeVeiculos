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

    /// <summary>
    /// Tamanho do vetor do rosto no método atual (embedding SFace, S33). Amostras de outro tamanho (a geometria de 468 pontos
    /// do método anterior, 1404 números) não servem mais: são ignoradas na leitura e apagadas quando o rosto é recadastrado.
    /// </summary>
    public const int TamanhoDoVetor = 128;

    public async Task<int> SalvarAsync(string pessoaId, IReadOnlyList<double> landmarks, CancellationToken cancellationToken)
    {
        var pessoa = Normalizar(pessoaId);

        // Cadastro no método atual: o rosto no formato antigo (que não compara mais) é apagado de vez.
        if (landmarks.Count == TamanhoDoVetor)
            await ApagarAmostrasNoFormatoAntigoAsync(pessoa, cancellationToken);

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

    private async Task ApagarAmostrasNoFormatoAntigoAsync(string pessoa, CancellationToken cancellationToken)
    {
        var existentes = await dbContext.Set<AmostraFacial>().Where(a => a.PessoaId == pessoa).ToListAsync(cancellationToken);
        var antigas = existentes.Where(a => Ler(a, pessoa).Length != TamanhoDoVetor).ToList();

        if (antigas.Count > 0)
        {
            dbContext.Set<AmostraFacial>().RemoveRange(antigas);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private double[] Ler(AmostraFacial amostra, string pessoa) =>
        JsonSerializer.Deserialize<double[]>(cifrador.Decifrar(amostra.Conteudo, pessoa)) ?? [];

    public async Task<IReadOnlyList<IReadOnlyList<double>>> ObterAsync(string pessoaId, CancellationToken cancellationToken)
    {
        var pessoa = Normalizar(pessoaId);

        var amostras = await dbContext.Set<AmostraFacial>()
            .AsNoTracking()
            .Where(a => a.PessoaId == pessoa)
            .ToListAsync(cancellationToken);

        // Só o método atual: quem tem o rosto no formato antigo aparece como "sem rosto" e precisa recadastrar.
        return amostras
            .OrderBy(a => a.CriadoEmUtc)
            .Select(a => Ler(a, pessoa))
            .Where(v => v.Length == TamanhoDoVetor)
            .Select(v => (IReadOnlyList<double>)v)
            .ToList();
    }

    public async Task<bool> ExisteAsync(string pessoaId, CancellationToken cancellationToken) =>
        (await ObterAsync(pessoaId, cancellationToken)).Count > 0;

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
