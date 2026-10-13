namespace LocadoraDeVeiculos.WebApi.Models.ModuloAutenticacao;

public record AutenticarComRostoRequest(string Email, string ImagemBase64, string? CodigoAutenticador = null);

public record CadastrarRostoRequest(string ImagemBase64, bool Consentimento = false, string? VersaoDoTexto = null);

public record TermoDeConsentimentoResponse(string Versao, string Texto, int RetencaoEmMeses);

public record CadastrarRostoResponse(int Amostras);

public record VerificarRostoRequest(string ImagemBase64);

public record VerificarRostoResponse(bool Confirmado, double Confianca);

public record StatusRostoResponse(bool Cadastrado);
