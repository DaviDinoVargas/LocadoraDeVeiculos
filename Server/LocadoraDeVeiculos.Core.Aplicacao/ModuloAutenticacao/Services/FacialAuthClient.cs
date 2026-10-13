using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;

// Cliente HTTP para o serviço Python de Machine Learning (MachineLeaning/FaceAuth),
// que guarda os landmarks faciais (MediaPipe FaceMesh) e faz a comparação.
// O backend .NET nunca confia numa alegação de "rosto verificado" vinda do
// Angular: é ele quem chama o serviço de ML diretamente (servidor-a-servidor) e
// decide se emite um token de acesso, exatamente como faz hoje com a senha.
public interface IFacialAuthClient
{
    Task<FacialVerifyResult> VerificarAsync(string personId, string imagemBase64, CancellationToken cancellationToken);
    Task<FacialEnrollResult> CadastrarAsync(string personId, string imagemBase64, CancellationToken cancellationToken);
    Task<bool> PossuiRostoAsync(string personId, CancellationToken cancellationToken);

    /// <summary>Apaga o rosto cadastrado. Devolve true se removeu ou se já não havia nada (idempotente).</summary>
    Task<bool> RemoverAsync(string personId, CancellationToken cancellationToken);
}

public class FacialAuthClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IArmazenamentoFacial armazenamento
) : IFacialAuthClient
{
    private const string NomeCliente = "MlApi";

    // Precisa bater com ML_INTERNAL_TOKEN no serviço de ML (shared/settings.py). TODO o módulo
    // /face/* exige esse cabeçalho: só esta API fala com ele, o navegador nunca.
    private string SegredoInterno => SegredoInternoDoMl.Resolver(configuration);

    // Prova de vida (S3). Fora de Development ela é OBRIGATÓRIA: se o ML responder sem tê-la feito (versão antiga, desligada),
    // a foto é recusada em vez de aceita. ANTISPOOF_REQUIRED=true/false força o comportamento.
    private bool ExigirProvaDeVida =>
        bool.TryParse(configuration["ANTISPOOF_REQUIRED"], out var forcado)
            ? forcado
            : !string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);

    private bool ProvaDeVidaReprovada(bool ativa, bool vivo) => ExigirProvaDeVida ? !(ativa && vivo) : ativa && !vivo;

    private HttpRequestMessage Requisicao(HttpMethod metodo, string url, object? corpo = null)
    {
        var mensagem = new HttpRequestMessage(metodo, url);
        if (corpo is not null)
            mensagem.Content = JsonContent.Create(corpo);
        mensagem.Headers.Add("X-Internal-Token", SegredoInterno);
        return mensagem;
    }

    public static string PersonIdParaEmail(string email) => $"usuario:{email.Trim().ToLowerInvariant()}";

    // O serviço de ML não guarda nada (S1): as amostras ficam cifradas no banco desta API. Na verificação
    // elas seguem junto com a imagem, só em memória e só nesta chamada interna.
    public async Task<FacialVerifyResult> VerificarAsync(string personId, string imagemBase64, CancellationToken cancellationToken)
    {
        var amostras = await armazenamento.ObterAsync(personId, cancellationToken);

        if (amostras.Count == 0)
            return new FacialVerifyResult(Encontrado: false, Bateu: false, Confianca: 0);

        var client = httpClientFactory.CreateClient(NomeCliente);

        using var mensagem = Requisicao(HttpMethod.Post, "/face/compare", new { imageBase64 = imagemBase64, samples = amostras });
        var resposta = await client.SendAsync(mensagem, cancellationToken);

        // 422 = nenhum rosto detectado; 400 = a imagem nem decodifica. Nos dois casos o usuário
        // precisa tirar outra foto, não é "rosto diferente".
        if (resposta.StatusCode is System.Net.HttpStatusCode.UnprocessableEntity or System.Net.HttpStatusCode.BadRequest)
            return new FacialVerifyResult(Encontrado: true, Bateu: false, Confianca: 0, NenhumRostoNaImagem: true);

        if (!resposta.IsSuccessStatusCode)
            return new FacialVerifyResult(Encontrado: true, Bateu: false, Confianca: 0);

        var corpo = await resposta.Content.ReadFromJsonAsync<FacialVerifyResponseDto>(cancellationToken: cancellationToken);

        var reprovadaNaVida = corpo is not null && ProvaDeVidaReprovada(corpo.ProvaDeVidaAtiva, corpo.Vivo);
        var bateu = (corpo?.Match ?? false) && !reprovadaNaVida;

        // Uso bem-sucedido zera a contagem de retenção de 12 meses (S1).
        if (bateu)
            await armazenamento.RegistrarUsoAsync(personId, cancellationToken);

        return new FacialVerifyResult(Encontrado: true, Bateu: bateu, Confianca: corpo?.Confidence ?? 0, ProvaDeVidaReprovada: reprovadaNaVida);
    }

    public async Task<FacialEnrollResult> CadastrarAsync(string personId, string imagemBase64, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(NomeCliente);

        using var mensagem = Requisicao(HttpMethod.Post, "/face/landmarks", new { imageBase64 = imagemBase64 });
        var resposta = await client.SendAsync(mensagem, cancellationToken);

        if (!resposta.IsSuccessStatusCode)
            return new FacialEnrollResult(Sucesso: false, Amostras: 0);

        var corpo = await resposta.Content.ReadFromJsonAsync<FacialLandmarksResponseDto>(cancellationToken: cancellationToken);

        if (corpo?.Landmarks is not { Count: > 0 })
            return new FacialEnrollResult(Sucesso: false, Amostras: 0);

        // Uma foto de foto/tela não pode virar o "rosto cadastrado".
        if (ProvaDeVidaReprovada(corpo.ProvaDeVidaAtiva, corpo.Vivo))
            return new FacialEnrollResult(Sucesso: false, Amostras: 0, ProvaDeVidaReprovada: true);

        var total = await armazenamento.SalvarAsync(personId, corpo.Landmarks, cancellationToken);

        return new FacialEnrollResult(Sucesso: true, Amostras: total);
    }

    public Task<bool> PossuiRostoAsync(string personId, CancellationToken cancellationToken) =>
        armazenamento.ExisteAsync(personId, cancellationToken);

    public async Task<bool> RemoverAsync(string personId, CancellationToken cancellationToken)
    {
        await armazenamento.RemoverAsync(personId, cancellationToken);

        // Idempotente: "já não havia nada" também é sucesso.
        return true;
    }

    private record FacialVerifyResponseDto(bool Match, double Confidence, bool ProvaDeVidaAtiva = false, bool Vivo = true);
    private record FacialLandmarksResponseDto(List<double> Landmarks, bool ProvaDeVidaAtiva = false, bool Vivo = true);
}

public record FacialVerifyResult(bool Encontrado, bool Bateu, double Confianca, bool NenhumRostoNaImagem = false, bool ProvaDeVidaReprovada = false);
public record FacialEnrollResult(bool Sucesso, int Amostras, bool ProvaDeVidaReprovada = false);
