using Microsoft.Extensions.Configuration;
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
public class FacialAuthClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    private const string NomeCliente = "MlApi";

    // Precisa bater com ML_INTERNAL_TOKEN em MachineLeaning/FaceAuth/router.py — é o que
    // impede qualquer um de chamar /face/enroll direto (o Angular expõe /api/ml/* sem
    // checar sessão) e plantar o próprio rosto no personId de outra pessoa.
    private string SegredoInterno => configuration["MlApi:InternalToken"] ?? "dev-only-shared-secret-troque-em-producao";

    public static string PersonIdParaEmail(string email) => $"usuario:{email.Trim().ToLowerInvariant()}";

    public async Task<FacialVerifyResult> VerificarAsync(string personId, string imagemBase64, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(NomeCliente);

        var resposta = await client.PostAsJsonAsync("/face/verify", new { personId, imageBase64 = imagemBase64 }, cancellationToken);

        if (resposta.StatusCode == System.Net.HttpStatusCode.NotFound)
            return new FacialVerifyResult(Encontrado: false, Bateu: false, Confianca: 0);

        if (!resposta.IsSuccessStatusCode)
            return new FacialVerifyResult(Encontrado: true, Bateu: false, Confianca: 0);

        var corpo = await resposta.Content.ReadFromJsonAsync<FacialVerifyResponseDto>(cancellationToken: cancellationToken);

        return new FacialVerifyResult(Encontrado: true, Bateu: corpo?.Match ?? false, Confianca: corpo?.Confidence ?? 0);
    }

    public async Task<FacialEnrollResult> CadastrarAsync(string personId, string imagemBase64, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(NomeCliente);

        using var mensagem = new HttpRequestMessage(HttpMethod.Post, "/face/enroll")
        {
            Content = JsonContent.Create(new { personId, imageBase64 = imagemBase64 })
        };
        mensagem.Headers.Add("X-Internal-Token", SegredoInterno);

        var resposta = await client.SendAsync(mensagem, cancellationToken);

        if (!resposta.IsSuccessStatusCode)
            return new FacialEnrollResult(Sucesso: false, Amostras: 0);

        var corpo = await resposta.Content.ReadFromJsonAsync<FacialEnrollResponseDto>(cancellationToken: cancellationToken);

        return new FacialEnrollResult(Sucesso: true, Amostras: corpo?.Amostras ?? 0);
    }

    private record FacialVerifyResponseDto(bool Match, double Confidence);
    private record FacialEnrollResponseDto(bool Ok, string PersonId, int Amostras);
}

public record FacialVerifyResult(bool Encontrado, bool Bateu, double Confianca);
public record FacialEnrollResult(bool Sucesso, int Amostras);
