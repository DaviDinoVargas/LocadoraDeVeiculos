namespace LocadoraDeVeiculos.WebApi.Models.ModuloAutenticacao;

public record AutenticarComRostoRequest(string Email, string ImagemBase64);

public record CadastrarRostoRequest(string ImagemBase64);

public record CadastrarRostoResponse(int Amostras);

public record VerificarRostoRequest(string ImagemBase64);

public record VerificarRostoResponse(bool Confirmado, double Confianca);

public record StatusRostoResponse(bool Cadastrado);
