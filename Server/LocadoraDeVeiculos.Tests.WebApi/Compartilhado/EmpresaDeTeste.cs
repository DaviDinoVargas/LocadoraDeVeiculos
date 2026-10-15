using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocadoraDeVeiculos.Tests.WebApi.Compartilhado;

/// <summary>Resposta da API já lida: status e corpo JSON (quando houver).</summary>
public sealed record Resposta(HttpStatusCode Status, JsonElement? Corpo)
{
    public bool Ok => (int)Status is >= 200 and < 300;

    /// <summary>Id devolvido por um POST de cadastro.</summary>
    public Guid Id => Corpo is { ValueKind: JsonValueKind.Object } c && c.TryGetProperty("id", out var id)
        ? id.GetGuid()
        : throw new AssertFailedException($"A resposta não trouxe um id (status {(int)Status}): {Corpo}");

    /// <summary>Mensagens de erro (a API devolve os erros de negócio como lista de textos).</summary>
    public IReadOnlyList<string> Erros => Corpo is { ValueKind: JsonValueKind.Array } a
        ? a.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
        : [];

    public string TextoDosErros => string.Join(" | ", Erros);

    /// <summary>Itens da propriedade "registros" de uma listagem.</summary>
    public IReadOnlyList<JsonElement> Registros => Corpo is { ValueKind: JsonValueKind.Object } c && c.TryGetProperty("registros", out var r)
        ? r.EnumerateArray().ToList()
        : [];

    public JsonElement Propriedade(string nome) =>
        Corpo is { ValueKind: JsonValueKind.Object } c && c.TryGetProperty(nome, out var p)
            ? p
            : throw new AssertFailedException($"A resposta não tem a propriedade '{nome}': {Corpo}");
}

/// <summary>
/// Uma empresa (tenant) nova dentro da API de teste, já autenticada. Cada teste cria a sua, então os dados de um não aparecem
/// no outro (o isolamento por empresa é o mesmo da produção) e a fábrica da API pode ser compartilhada pela classe de teste.
/// Os atalhos <c>Criar*Async</c> montam as dependências de um aluguel (grupo, plano, carro, cliente, condutor) com dados válidos.
/// </summary>
public sealed class EmpresaDeTeste : IDisposable
{
    private static int _sequencia;

    public HttpClient Http { get; }
    public Guid EmpresaId { get; }

    private EmpresaDeTeste(HttpClient http, Guid empresaId) => (Http, EmpresaId) = (http, empresaId);

    public static async Task<EmpresaDeTeste> CriarAsync(ApiFactory fabrica, string cargo = "Empresa", Guid? empresaId = null)
    {
        // Sem empresaId, o usuário criado É a empresa (como no cadastro real): o repositório de vários módulos faz Include(Empresa),
        // então o id do tenant precisa ser de um usuário que existe.
        var token = await fabrica.CriarTokenAsync(cargo, empresaId);
        var empresa = Guid.Parse(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token).Claims.First(c => c.Type == "EmpresaId").Value);
        return new EmpresaDeTeste(fabrica.CriarClienteAutenticado(token), empresa);
    }

    /// <summary>Um usuário de outro cargo (ex.: Funcionario) na MESMA empresa.</summary>
    public static Task<EmpresaDeTeste> DoMesmoTenantAsync(ApiFactory fabrica, EmpresaDeTeste dona, string cargo) =>
        CriarAsync(fabrica, cargo, dona.EmpresaId);

    public async Task<Resposta> EnviarAsync(HttpMethod metodo, string rota, object? corpo = null)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo);

        using var resposta = await Http.SendAsync(requisicao);
        var texto = await resposta.Content.ReadAsStringAsync();
        JsonElement? json = null;
        if (!string.IsNullOrWhiteSpace(texto))
        {
            try { json = JsonDocument.Parse(texto).RootElement.Clone(); } catch (JsonException) { /* corpo que não é JSON */ }
        }

        return new Resposta(resposta.StatusCode, json);
    }

    public Task<Resposta> PostAsync(string rota, object? corpo = null) => EnviarAsync(HttpMethod.Post, rota, corpo);
    public Task<Resposta> PutAsync(string rota, object corpo) => EnviarAsync(HttpMethod.Put, rota, corpo);
    public Task<Resposta> GetAsync(string rota) => EnviarAsync(HttpMethod.Get, rota);
    public Task<Resposta> DeleteAsync(string rota) => EnviarAsync(HttpMethod.Delete, rota);

    // ---- dados válidos -------------------------------------------------------------------------------------------------

    public static string Placa() => $"{(char)('A' + Interlocked.Increment(ref _sequencia) % 26)}{(char)('A' + (_sequencia / 26) % 26)}{(char)('A' + (_sequencia / 676) % 26)}1B{_sequencia % 90 + 10}";

    private static int DigitoVerificador(IReadOnlyList<int> digitos, int[] pesos)
    {
        var resto = digitos.Select((d, i) => d * pesos[i]).Sum() % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    /// <summary>CPF único e com os dois dígitos verificadores corretos (a API confere os dígitos, não só o formato).</summary>
    public static string Cpf()
    {
        var d = (100_000_000 + Interlocked.Increment(ref _sequencia)).ToString().Select(c => c - '0').ToList();
        d.Add(DigitoVerificador(d, [10, 9, 8, 7, 6, 5, 4, 3, 2]));
        d.Add(DigitoVerificador(d, [11, 10, 9, 8, 7, 6, 5, 4, 3, 2]));
        var s = string.Concat(d);
        return $"{s[..3]}.{s[3..6]}.{s[6..9]}-{s[9..]}";
    }

    /// <summary>CNPJ único e com os dois dígitos verificadores corretos.</summary>
    public static string Cnpj()
    {
        var d = (10_000_000 + Interlocked.Increment(ref _sequencia)).ToString().Select(c => c - '0').Concat([0, 0, 0, 1]).ToList();
        d.Add(DigitoVerificador(d, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]));
        d.Add(DigitoVerificador(d, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]));
        var s = string.Concat(d);
        return $"{s[..2]}.{s[2..5]}.{s[5..8]}/{s[8..12]}-{s[12..]}";
    }

    public static string Cnh() => (10_000_000_000L + Interlocked.Increment(ref _sequencia)).ToString();

    public Task<Resposta> CriarGrupoAsync(string? nome = null) =>
        PostAsync("/api/grupos-automovel", new { nome = nome ?? $"Grupo {Guid.NewGuid():N}"[..14], descricao = "Grupo de teste" });

    public Task<Resposta> CriarPlanoAsync(Guid grupoId, decimal diaria = 100m, decimal porKm = 2m, int kmLivre = 100) =>
        PostAsync("/api/planos-cobranca", new { grupoAutomovelId = grupoId, nome = "Plano de teste", precoDiaria = diaria, precoPorKm = porKm, kmLivreLimite = kmLivre });

    public Task<Resposta> CriarAutomovelAsync(Guid grupoId, string? placa = null) =>
        PostAsync("/api/automoveis", new
        {
            placa = placa ?? Placa(), marca = "Fiat", cor = "Prata", modelo = "Uno", tipoCombustivel = "Gasolina",
            capacidadeTanque = 50m, ano = 2022, foto = (string?)null, grupoAutomovelId = grupoId
        });

    public Task<Resposta> CriarClienteAsync(string? nome = null, string? cpf = null, string? cnh = null) =>
        PostAsync("/api/clientes/pessoa-fisica", new
        {
            nome = nome ?? "Cliente de Teste", cpf = cpf ?? Cpf(), rg = "12.345.678-9", cnh = cnh ?? Cnh(),
            validadeCnh = DateTime.UtcNow.AddYears(2), telefone = "(11) 99999-0000", email = "cliente@teste.com", endereco = "Rua dos Testes, 100"
        });

    public Task<Resposta> CriarCondutorAsync(Guid clienteId, DateTime? validadeCnh = null, string? cpf = null, string? cnh = null) =>
        PostAsync("/api/condutores", new
        {
            nome = "Condutor de Teste", email = "condutor@teste.com", cpf = cpf ?? Cpf(), cnh = cnh ?? Cnh(),
            validadeCnh = validadeCnh ?? DateTime.UtcNow.AddYears(2), telefone = "(11) 98888-0000", clienteId
        });

    /// <summary>Tudo o que um aluguel precisa: grupo com plano, um carro, cliente e condutor da empresa.</summary>
    public async Task<Cenario> MontarCenarioAsync(int carros = 1)
    {
        var grupo = (await CriarGrupoAsync()).Id;
        await CriarPlanoAsync(grupo);
        var automoveis = new List<Guid>();
        for (var i = 0; i < carros; i++)
            automoveis.Add((await CriarAutomovelAsync(grupo)).Id);
        var cliente = (await CriarClienteAsync()).Id;
        var condutor = (await CriarCondutorAsync(cliente)).Id;
        return new Cenario(grupo, automoveis, cliente, condutor);
    }

    public static object CorpoDeAluguel(Cenario c, Guid? automovel = null, int diasAteSaida = 5, int duracao = 3, string[]? taxas = null, string? cupom = null)
    {
        var saida = DateTimeOffset.UtcNow.AddDays(diasAteSaida);
        return new
        {
            condutorId = c.Condutor, automovelId = automovel ?? c.Automoveis[0], clienteId = c.Cliente, dataSaida = saida,
            dataRetornoPrevisto = saida.AddDays(duracao), valorPrevisto = 300m, taxasServicosIds = taxas ?? [], cupomCodigo = cupom
        };
    }

    public void Dispose() => Http.Dispose();
}

public sealed record Cenario(Guid Grupo, IReadOnlyList<Guid> Automoveis, Guid Cliente, Guid Condutor);
