namespace LocadoraDeVeiculos.WebApi.Models.ModuloAutenticacao;

public record AutenticarUsuarioRequest(string Email, string Senha, string? CodigoAutenticador = null);

public record SolicitarRecuperacaoRequest(string Email);

public record RedefinirSenhaRequest(string Email, string Token, string NovaSenha, string ConfirmarNovaSenha);

public record AtivarDoisFatoresRequest(string Codigo);

public record DesativarDoisFatoresRequest(string Senha, string Codigo);

public record StatusDoisFatoresResponse(bool Habilitado, int CodigosDeRecuperacaoRestantes);

public record ConfiguracaoDoisFatoresResponse(string Chave, string Uri);

public record CodigosDeRecuperacaoResponse(IReadOnlyList<string> Codigos);
