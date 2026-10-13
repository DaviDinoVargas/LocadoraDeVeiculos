namespace LocadoraDeVeiculos.WebApi.Models.ModuloAutenticacao;

public record AlterarSenhaRequest(string SenhaAtual, string NovaSenha, string ConfirmarNovaSenha);
