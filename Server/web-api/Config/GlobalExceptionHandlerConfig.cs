using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Net;

namespace LocadoraDeVeiculos.WebApi.Config;

public static class GlobalExceptionHandlerConfig
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseExceptionHandler(builder =>
        {
            builder.Run(async httpContext =>
            {
                var gerenciadorExcecoes = httpContext.Features.Get<IExceptionHandlerFeature>();

                if (gerenciadorExcecoes is null) return;

                // Erros do cliente que o Kestrel detecta antes do controller (corpo acima do limite, cabeçalho
                // malformado...) chegam aqui como BadHttpRequestException: são 4xx (413, 400...), não falha do servidor.
                var erroDoCliente = gerenciadorExcecoes.Error as BadHttpRequestException;
                var status = erroDoCliente is not null ? erroDoCliente.StatusCode : (int)HttpStatusCode.InternalServerError;

                httpContext.Response.StatusCode = status;
                httpContext.Response.ContentType = "application/json";

                var objeto = new
                {
                    Sucesso = false,
                    Erros = new string[]
                    {
                        status == StatusCodes.Status413PayloadTooLarge ? "O corpo da requisição excede o tamanho permitido."
                        : erroDoCliente is not null ? "Requisição inválida."
                        : "Erro interno do servidor"
                    }
                };

                var respostaJson = JsonSerializer.Serialize(objeto);

                await httpContext.Response.WriteAsync(respostaJson);
            });
        });
    }
}