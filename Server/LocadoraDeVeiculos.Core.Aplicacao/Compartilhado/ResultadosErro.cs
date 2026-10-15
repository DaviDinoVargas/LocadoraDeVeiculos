using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentResults;

namespace LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;

public abstract class ResultadosErro
{
    public static Error RequisicaoInvalidaErro(string erro)
    {
        return new Error("Requisição inválida")
            .CausedBy(erro)
            .WithMetadata("TipoErro", "RequisicaoInvalida");
    }

    public static Error RequisicaoInvalidaErro(IEnumerable<string> erros)
    {
        return new Error("Requisição inválida")
            .CausedBy(erros)
            .WithMetadata("TipoErro", "RequisicaoInvalida");
    }

    public static Error RegistroDuplicadoErro(string mensagemErro)
    {
        return new Error("Registro duplicado")
            .CausedBy(mensagemErro)
            .WithMetadata("TipoErro", "RegistroDuplicado");
    }

    public static Error RegistroNaoEncontradoErro(Guid id)
    {
        return new Error("Registro não encontrado")
            .CausedBy("Não foi possível obter o registro com o ID: " + id)
            .WithMetadata("TipoErro", "RegistroNaoEncontrado");
    }

    public static Error RegistroNaoEncontradoErro(string registro)
    {
        return new Error("Registro não encontrado")
            .CausedBy("Não foi possível obter o registro: " + registro)
            .WithMetadata("TipoErro", "RegistroNaoEncontrado");
    }


    public static Error ExclusaoBloqueadaErro(string mensagemErro)
    {
        return new Error("Exclusão bloqueada")
            .CausedBy(mensagemErro)
            .WithMetadata("TipoErro", "ExclusaoBloqueada");
    }

    public static Error EstadoInvalidoErro(string mensagemErro)
    {
        return new Error("Estado inválido para esta operação")
            .CausedBy(mensagemErro)
            .WithMetadata("TipoErro", "EstadoInvalido");
    }


    public static Error NaoAutorizadoErro(string mensagemErro)
    {
        return new Error("Não autorizado")
            .CausedBy(mensagemErro)
            .WithMetadata("TipoErro", "NaoAutorizado");
    }

    /// <summary>A senha estava certa, mas a conta tem 2FA e o código não veio. O cliente responde com o código (HTTP 428).</summary>
    public static Error SegundoFatorObrigatorioErro()
    {
        return new Error("Segundo fator obrigatório")
            .CausedBy("Informe o código do aplicativo autenticador.")
            .WithMetadata("TipoErro", "SegundoFatorObrigatorio");
    }

    public static Error ExcecaoInternaErro(Exception ex)
    {
        // Corrida de cadastro: dois pedidos iguais passaram juntos pela checagem e o índice único do banco barrou o segundo.
        // Para quem chamou, é o mesmo que "já existe": 409 com mensagem fixa (nada do texto do SQL vai para a resposta).
        if (ViolacaoDeUnicidade.Eh(ex))
            return RegistroDuplicadoErro("Já existe um registro com estes dados (outro pedido idêntico acabou de ser gravado).");

        // Importante: NÃO usar .CausedBy(ex) aqui. FluentResults guarda ex.Message como um
        // "Reason" que o MainController serializa de volta na resposta HTTP — isso vazaria
        // detalhes internos (mensagens de SQL, caminhos, stack) para quem chama a API.
        // A exceção completa já é logada (ILogger) em cada handler que a captura; aqui só
        // guardamos a mensagem genérica que de fato deve chegar ao cliente.
        return new Error("Ocorreu um erro interno do servidor")
            .WithMetadata("TipoErro", "ExcecaoInterna");
    }
}
