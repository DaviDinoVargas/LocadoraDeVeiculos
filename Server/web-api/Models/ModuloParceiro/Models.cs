namespace LocadoraDeVeiculos.WebApi.Models.ModuloParceiro
{
    public record CadastrarParceiroRequest(string Nome, string Cnpj, string Categoria);

    public record CadastrarParceiroResponse(Guid Id);

    public record EditarParceiroRequest(string Nome, string Cnpj, string Categoria, bool Ativo);

    public record EditarParceiroResponse(string Nome, string Cnpj, string Categoria, bool Ativo);

    public record ExcluirParceiroResponse();

    public record SelecionarParceirosResponse(IReadOnlyList<SelecionarParceirosDto> Registros);

    public record SelecionarParceirosDto(Guid Id, string Nome, string Cnpj, string Categoria, bool Ativo);

    public record SelecionarParceiroPorIdResponse(Guid Id, string Nome, string Cnpj, string Categoria, bool Ativo);
}
