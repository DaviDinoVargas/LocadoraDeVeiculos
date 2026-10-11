using Microsoft.Extensions.Configuration;
using System;
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

public class FacialAuthClient(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IFacialAuthClient
{
    private const string NomeCliente = "MlApi";

    // Precisa bater com ML_INTERNAL_TOKEN no serviço de ML (shared/settings.py). TODO o módulo
    // /face/* exige esse cabeçalho: só esta API fala com ele, o navegador nunca. Sem isso
    // qualquer um que alcançasse o proxy do Angular poderia plantar o próprio rosto no personId
    // de outra pessoa (enroll), apagar o rosto dela (delete) ou usar /verify como oráculo.
    private string SegredoInterno => configuration["MlApi:InternalToken"] ?? "dev-only-shared-secret-troque-em-producao";

    private HttpRequestMessage Requisicao(HttpMethod metodo, string url, object? corpo = null)
    {
        var mensagem = new HttpRequestMessage(metodo, url);
        if (corpo is not null)
            mensagem.Content = JsonContent.Create(corpo);
        mensagem.Headers.Add("X-Internal-Token", SegredoInterno);
        return mensagem;
    }

    public static string PersonIdParaEmail(string email) => $"usuario:{email.Trim().ToLowerInvariant()}";

    public async Task<FacialVerifyResult> VerificarAsync(string personId, string imagemBase64, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(NomeCliente);

        using var mensagem = Requisicao(HttpMethod.Post, "/face/verify", new { personId, imageBase64 = imagemBase64 });
        var resposta = await client.SendAsync(mensagem, cancellationToken);

        if (resposta.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new FacialVerifyResult(Encontrado: false, Bateu: false, Confianca: 0);

        // 422 = nenhum rosto detectado; 400 = a imagem nem decodifica. Nos dois casos o usuário
        // precisa tirar outra foto, não é "rosto diferente".
        if (resposta.StatusCode is System.Net.HttpStatusCode.UnprocessableEntity or System.Net.HttpStatusCode.BadRequest)
            return new FacialVerifyResult(Encontrado: true, Bateu: false, Confianca: 0, NenhumRostoNaImagem: true);

        if (!resposta.IsSuccessStatusCode)
            return new FacialVerifyResult(Encontrado: true, Bateu: false, Confianca: 0);

        var corpo = await resposta.Content.ReadFromJsonAsync<FacialVerifyResponseDto>(cancellationToken: cancellationToken);

        return new FacialVerifyResult(Encontrado: true, Bateu: corpo?.Match ?? false, Confianca: corpo?.Confidence ?? 0);
    }

    public async Task<FacialEnrollResult> CadastrarAsync(string personId, string imagemBase64, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(NomeCliente);

        using var mensagem = Requisicao(HttpMethod.Post, "/face/enroll", new { personId, imageBase64 = imagemBase64 });

        var resposta = await client.SendAsync(mensagem, cancellationToken);

        if (!resposta.IsSuccessStatusCode)
            return new FacialEnrollResult(Sucesso: false, Amostras: 0);

        var corpo = await resposta.Content.ReadFromJsonAsync<FacialEnrollResponseDto>(cancellationToken: cancellationToken);

        return new FacialEnrollResult(Sucesso: true, Amostras: corpo?.Amostras ?? 0);
    }

    public async Task<bool> PossuiRostoAsync(string personId, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(NomeCliente);

        using var mensagem = Requisicao(HttpMethod.Get, $"/face/status/{Uri.EscapeDataString(personId)}");
        var resposta = await client.SendAsync(mensagem, cancellationToken);
        resposta.EnsureSuccessStatusCode();

        var corpo = await resposta.Content.ReadFromJsonAsync<FacialStatusResponseDto>(cancellationToken: cancellationToken);

        return corpo?.Cadastrado ?? false;
    }

    public async Task<bool> RemoverAsync(string personId, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(NomeCliente);

        using var mensagem = Requisicao(HttpMethod.Delete, $"/face/{Uri.EscapeDataString(personId)}");
        var resposta = await client.SendAsync(mensagem, cancellationToken);

        return resposta.IsSuccessStatusCode || resposta.StatusCode == System.Net.HttpStatusCode.NotFound;
    }

    private record FacialStatusResponseDto(string PersonId, bool Cadastrado);
    private record FacialVerifyResponseDto(bool Match, double Confidence);
    private record FacialEnrollResponseDto(bool Ok, string PersonId, int Amostras);
}

public record FacialVerifyResult(bool Encontrado, bool Bateu, double Confianca, bool NenhumRostoNaImagem = false);
public record FacialEnrollResult(bool Sucesso, int Amostras);
