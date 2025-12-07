namespace LocadoraDeVeiculos.WebApi.Models.ModuloPlanoCobranca
{
    public record CadastrarPlanoCobrancaRequest(
     Guid GrupoAutomovelId,
     string Nome,
     decimal PrecoDiaria,
     decimal PrecoPorKm,
     int KmLivreLimite);

    public record CadastrarPlanoCobrancaResponse(Guid Id);

    public record EditarPlanoCobrancaRequest(
        Guid GrupoAutomovelId,
        string Nome,
        decimal PrecoDiaria,
        decimal PrecoPorKm,
        int KmLivreLimite);

    public record EditarPlanoCobrancaResponse(
        string Nome, decimal PrecoDiaria, decimal PrecoPorKm, int KmLivreLimite);

    public record SelecionarPlanoCobrancaPorIdResponse(
        Guid Id, Guid GrupoAutomovelId, string Nome,
        decimal PrecoDiaria, decimal PrecoPorKm, int KmLivreLimite);

    public record SelecionarPlanosCobrancaResponse(
        IReadOnlyList<SelecionarPlanosCobrancaDto> Registros);

    public record ExcluirPlanoCobrancaResponse();

}
