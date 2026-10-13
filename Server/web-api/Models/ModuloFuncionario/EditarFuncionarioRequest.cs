namespace LocadoraDeVeiculos.WebApi.Models.ModuloFuncionario;

public record EditarFuncionarioRequest(
    string NomeCompleto,
    string Cpf,
    decimal Salario,
    DateTimeOffset AdmissaoEmUtc,
    string? Senha = null
);

public record EditarFuncionarioResponse(
    string NomeCompleto,
    string Cpf,
    decimal Salario,
    DateTimeOffset AdmissaoEmUtc
);