namespace LocadoraDeVeiculos.WebApi.Models.ModuloCupom
{
    public record CadastrarCupomRequest(
        string Codigo,
        string Descricao,
        string TipoDesconto,
        decimal ValorDesconto,
        DateTimeOffset ValidoAte,
        int? LimiteUsos,
        Guid? ParceiroId
    );

    public record CadastrarCupomResponse(Guid Id);

    public record EditarCupomRequest(
        string Codigo,
        string Descricao,
        string TipoDesconto,
        decimal ValorDesconto,
        DateTimeOffset ValidoAte,
        int? LimiteUsos,
        Guid? ParceiroId,
        bool Ativo
    );

    public record EditarCupomResponse(string Codigo, string Descricao, decimal ValorDesconto, DateTimeOffset ValidoAte);

    public record ExcluirCupomResponse();

    public record SelecionarCuponsResponse(IReadOnlyList<SelecionarCuponsDto> Registros);

    public record SelecionarCuponsDto(
        Guid Id, string Codigo, string Descricao, string TipoDesconto, decimal ValorDesconto,
        DateTimeOffset ValidoAte, int? LimiteUsos, int UsosAtuais, bool Ativo);

    public record SelecionarCupomPorIdResponse(
        Guid Id, string Codigo, string Descricao, string TipoDesconto, decimal ValorDesconto,
        DateTimeOffset ValidoAte, int? LimiteUsos, int UsosAtuais, Guid? ParceiroId, string? ParceiroNome, bool Ativo);

    public record ValidarCupomRequest(string Codigo, decimal ValorBase);

    public record ValidarCupomResponse(bool Valido, string? MotivoInvalido, decimal ValorDesconto, decimal ValorComDesconto);
}
