namespace LocadoraDeVeiculos.WebApi.Models.ModuloDesafioCupom
{
    public record CadastrarDesafioCupomRequest(
        string Nome, string Descricao, int MetaQuantidadeAlugueis, int PeriodoDias, Guid CupomRecompensaId);

    public record CadastrarDesafioCupomResponse(Guid Id);

    public record EditarDesafioCupomRequest(
        string Nome, string Descricao, int MetaQuantidadeAlugueis, int PeriodoDias, Guid CupomRecompensaId, bool Ativo);

    public record EditarDesafioCupomResponse(string Nome, string Descricao, int MetaQuantidadeAlugueis, int PeriodoDias);

    public record ExcluirDesafioCupomResponse();

    public record SelecionarDesafiosCupomResponse(IReadOnlyList<SelecionarDesafiosCupomDto> Registros);

    public record SelecionarDesafiosCupomDto(
        Guid Id, string Nome, string Descricao, int MetaQuantidadeAlugueis, int PeriodoDias,
        string CupomRecompensaCodigo, bool Ativo);

    public record SelecionarDesafioCupomPorIdResponse(
        Guid Id, string Nome, string Descricao, int MetaQuantidadeAlugueis, int PeriodoDias,
        Guid CupomRecompensaId, string CupomRecompensaCodigo, bool Ativo);

    public record VerificarDesafiosClienteResponse(IReadOnlyList<ProgressoDesafioDto> Progresso);

    public record ProgressoDesafioDto(
        Guid DesafioId, string Nome, int Meta, int Progresso, bool Cumprido, string? CupomLiberadoCodigo);
}
