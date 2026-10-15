using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace LocadoraDeVeiculos.Tests.WebApi.Compartilhado;

/// <summary>Quem pode chamar um endpoint, calculado pelas mesmas regras do ASP.NET (ver <see cref="Calcular"/>).</summary>
public sealed record AcessoDoEndpoint(bool Anonimo, bool ExigeLogin, IReadOnlyList<string>? Papeis)
{
    /// <summary>Qualquer pessoa, logada ou não, consegue chamar: tem [AllowAnonymous] OU não tem nenhum [Authorize] (a API não define política padrão, então "sem atributo" = aberto).</summary>
    public bool AbertoAoPublico => Anonimo || !ExigeLogin;

    /// <summary>Texto para a tabela: "Público (...)", "Qualquer usuário autenticado", "Empresa", "Empresa ou Funcionario"...</summary>
    public string Rotulo =>
        Anonimo ? "Público ([AllowAnonymous])"
        : !ExigeLogin ? "Público (sem [Authorize])"
        : Papeis is null ? "Qualquer usuário autenticado"
        : string.Join(" ou ", Papeis);

    /// <summary>
    /// Regras do ASP.NET: [AllowAnonymous] em qualquer ponto (classe ou método) libera o endpoint; cada [Authorize] é um requisito a mais
    /// (o do método SOMA ao da classe), então a lista de papéis efetiva é a INTERSEÇÃO dos papéis de todos os [Authorize] que listam papéis.
    /// </summary>
    public static AcessoDoEndpoint Calcular(IEnumerable<object> metadados)
    {
        var lista = metadados.ToList();
        var autorizacoes = lista.OfType<IAuthorizeData>().ToList();
        HashSet<string>? papeis = null;

        foreach (var autorizacao in autorizacoes.Where(a => !string.IsNullOrWhiteSpace(a.Roles)))
        {
            var deste = autorizacao.Roles!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
            if (papeis is null) papeis = deste;
            else papeis.IntersectWith(deste);
        }

        return new AcessoDoEndpoint(lista.OfType<IAllowAnonymous>().Any(), autorizacoes.Count > 0, papeis?.Order().ToList());
    }
}

/// <summary>Uma ação de controller lida por reflexão: verbo, rota resolvida, quem acessa, filtros e assinatura.</summary>
public sealed record EndpointMapeado(
    string Verbo, string Rota, Type Controller, MethodInfo Acao, AcessoDoEndpoint Acesso,
    bool ExigeCabecalhoCliente, string? LimiteDeTaxa, string Parametros, string Retorno)
{
    public string Chave => $"{Verbo} {Rota}";
    public string Nome => $"{Controller.Name}.{Acao.Name}";
}

/// <summary>
/// Lê os controllers com <c>System.Reflection</c> (o ASP.NET faz o mesmo para montar as rotas) e monta a lista de endpoints. Serve de
/// contraprova da API oficial <see cref="IApiDescriptionGroupCollectionProvider"/>: se as duas listas baterem, a leitura por reflexão está certa
/// e pode ser usada para varrer a superfície inteira atrás de endpoints sem autorização.
/// </summary>
public static class MapaDeEndpoints
{
    private static readonly Regex Restricoes = new(@"\{([^}:?]+)[^}]*\}", RegexOptions.Compiled);

    public static IReadOnlyList<EndpointMapeado> PorReflexao(Assembly assembly) => PorReflexao(assembly.GetTypes());

    public static IReadOnlyList<EndpointMapeado> PorReflexao(IEnumerable<Type> tipos)
    {
        var encontrados = new List<EndpointMapeado>();

        var controllers = tipos.Where(t => t is { IsClass: true, IsAbstract: false }
                                           && typeof(ControllerBase).IsAssignableFrom(t)
                                           && !t.IsDefined(typeof(NonControllerAttribute), inherit: true));

        foreach (var controller in controllers)
        {
            var prefixo = controller.GetCustomAttributes<RouteAttribute>(inherit: true).FirstOrDefault()?.Template;
            var doTipo = controller.GetCustomAttributes(inherit: true);

            // DeclaredOnly: as ações ficam declaradas no próprio controller (MainController só tem utilitários protegidos).
            var metodos = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsDefined(typeof(NonActionAttribute), inherit: true));

            foreach (var metodo in metodos)
            {
                var doMetodo = metodo.GetCustomAttributes(inherit: true);
                var acesso = AcessoDoEndpoint.Calcular(doTipo.Concat(doMetodo));
                var limite = (doMetodo.OfType<EnableRateLimitingAttribute>().FirstOrDefault() ?? doTipo.OfType<EnableRateLimitingAttribute>().FirstOrDefault())?.PolicyName;
                var exigeCabecalho = doMetodo.Any(a => a.GetType().Name == "ExigirCabecalhoClienteAttribute");

                foreach (var http in metodo.GetCustomAttributes<HttpMethodAttribute>(inherit: true))
                foreach (var verbo in http.HttpMethods)
                {
                    var rota = Resolver(prefixo, http.Template, controller, metodo);
                    encontrados.Add(new EndpointMapeado(verbo, rota, controller, metodo, acesso, exigeCabecalho, limite, Parametros(metodo), Retorno(metodo)));
                }
            }
        }

        return encontrados.OrderBy(e => e.Rota, StringComparer.Ordinal).ThenBy(e => e.Verbo, StringComparer.Ordinal).ToList();
    }

    /// <summary>Os endpoints da API oficial do ASP.NET (a mesma que o Swagger usa), no mesmo formato.</summary>
    public static IReadOnlyList<(string Verbo, string Rota, AcessoDoEndpoint Acesso, MethodInfo? Acao)> PelaApiOficial(IApiDescriptionGroupCollectionProvider provedor) =>
        provedor.ApiDescriptionGroups.Items
            .SelectMany(g => g.Items)
            .Where(d => d.HttpMethod is not null)
            .Select(d => (
                Verbo: d.HttpMethod!,
                Rota: Normalizar(d.RelativePath ?? ""),
                Acesso: AcessoDoEndpoint.Calcular(d.ActionDescriptor.EndpointMetadata ?? []),
                Acao: (d.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo))
            .OrderBy(e => e.Rota, StringComparer.Ordinal).ThenBy(e => e.Verbo, StringComparer.Ordinal)
            .ToList();

    /// <summary>minúsculas, sem barra nas pontas e sem restrições de parâmetro ("{id:guid}" vira "{id}") para comparar as duas fontes.</summary>
    public static string Normalizar(string rota) => Restricoes.Replace(rota.Trim('/'), "{$1}").ToLowerInvariant();

    private static string Resolver(string? prefixo, string? template, Type controller, MethodInfo metodo)
    {
        string bruta;
        if (template is not null && (template.StartsWith('/') || template.StartsWith("~/")))
            bruta = template.TrimStart('~');
        else
            bruta = string.Join('/', new[] { prefixo, template }.Where(p => !string.IsNullOrEmpty(p)));

        bruta = bruta.Replace("[controller]", controller.Name.Replace("Controller", ""), StringComparison.OrdinalIgnoreCase)
                     .Replace("[action]", metodo.Name, StringComparison.OrdinalIgnoreCase);
        return Normalizar(bruta);
    }

    private static string Parametros(MethodInfo metodo) =>
        string.Join(", ", metodo.GetParameters()
            .Where(p => p.ParameterType != typeof(CancellationToken))
            .Select(p => $"{p.Name}: {NomeDoTipo(p.ParameterType)}{Origem(p)}"));

    private static string Origem(ParameterInfo parametro)
    {
        var origem = parametro.GetCustomAttributes(inherit: true).OfType<IBindingSourceMetadata>().FirstOrDefault()?.BindingSource;
        return origem is null ? "" : $" [{origem.DisplayName}]";
    }

    private static string Retorno(MethodInfo metodo) => NomeDoTipo(metodo.ReturnType);

    private static string NomeDoTipo(Type tipo)
    {
        if (!tipo.IsGenericType) return tipo.Name;
        var nome = tipo.Name[..tipo.Name.IndexOf('`')];
        return $"{nome}<{string.Join(", ", tipo.GetGenericArguments().Select(NomeDoTipo))}>";
    }

    /// <summary>Tabela em Markdown, uma linha por endpoint, para o relatório de superfície de ataque e a documentação.</summary>
    public static string EmMarkdown(IEnumerable<EndpointMapeado> endpoints)
    {
        var lista = endpoints.ToList();
        var texto = new StringBuilder();
        texto.AppendLine("# Endpoints da Web API lidos por reflexão (System.Reflection)");
        texto.AppendLine();
        texto.AppendLine($"{lista.Count} endpoints · {lista.Count(e => e.Acesso.AbertoAoPublico)} abertos ao público.");
        texto.AppendLine();
        texto.AppendLine("| Verbo | Rota | Ação | Quem acessa | Limite de taxa | Exige cabeçalho do cliente | Parâmetros | Retorno |");
        texto.AppendLine("|---|---|---|---|---|---|---|---|");

        foreach (var e in lista)
            texto.AppendLine($"| {e.Verbo} | `/{e.Rota}` | {e.Nome} | {e.Acesso.Rotulo} | {e.LimiteDeTaxa ?? "—"} | {(e.ExigeCabecalhoCliente ? "sim" : "—")} | {(e.Parametros.Length == 0 ? "—" : e.Parametros)} | {e.Retorno} |");

        return texto.ToString();
    }
}
