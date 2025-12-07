namespace LocadoraDeVeiculos.WebApi.Models.ModuloGrupoAutomovel
{
    public record CadastrarGrupoAutomovelRequest(
        string Nome,
        string? Descricao
    );

    public record CadastrarGrupoAutomovelResponse(Guid Id);

    public record EditarGrupoAutomovelRequest(
        string Nome,
        string? Descricao
    );

    public record EditarGrupoAutomovelResponse(
        string Nome,
        string? Descricao
    );

    public record ExcluirGrupoAutomovelRequest(Guid Id);

    public record ExcluirGrupoAutomovelResponse();

    public record SelecionarGruposAutomovelResponse(IReadOnlyList<SelecionarGruposAutomovelDto> Registros);

    public record SelecionarGruposAutomovelDto(
        Guid Id,
        string Nome,
        string? Descricao
    );

    public record SelecionarGrupoAutomovelPorIdResponse(
        Guid Id,
        string Nome,
        string? Descricao
    );
}