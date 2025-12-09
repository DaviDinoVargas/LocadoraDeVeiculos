namespace LocadoraDeVeiculos.WebApi.Models.ModuloTaxaServico
{
    // Requests
    public record CadastrarTaxaServicoRequest(
        string Nome,
        decimal Preco,
        string TipoCalculo
    );

    public record EditarTaxaServicoRequest(
        string Nome,
        decimal Preco,
        string TipoCalculo
    );

    public record ExcluirTaxaServicoRequest(Guid Id);

    // Responses
    public record CadastrarTaxaServicoResponse(Guid Id);

    public record EditarTaxaServicoResponse(
        string Nome,
        decimal Preco,
        string TipoCalculo
    );

    public record ExcluirTaxaServicoResponse();

    public record SelecionarTaxasServicoResponse(IReadOnlyList<SelecionarTaxasServicoDto> Registros);

    public record SelecionarTaxaServicoPorIdResponse(
        Guid Id,
        string Nome,
        decimal Preco,
        string TipoCalculo
    );

    public record SelecionarTaxasServicoPorTipoResponse(IReadOnlyList<SelecionarTaxasServicoDto> Registros);

    // DTOs
    public record SelecionarTaxasServicoDto(
        Guid Id,
        string Nome,
        decimal Preco,
        string TipoCalculo
    );
}