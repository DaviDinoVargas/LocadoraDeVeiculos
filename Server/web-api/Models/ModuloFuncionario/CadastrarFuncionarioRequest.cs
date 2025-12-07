namespace LocadoraDeVeiculos.WebApi.Models.ModuloFuncionario;

public record CadastrarFuncionarioRequest(
    string NomeCompleto,
    string Cpf,
    string Email,
    string Senha,
    string ConfirmarSenha,
    decimal Salario,
    DateTimeOffset AdmissaoEmUtc
);

public record CadastrarFuncionarioResponse(Guid Id);
