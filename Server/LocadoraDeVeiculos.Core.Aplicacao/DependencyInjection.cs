using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using FluentValidation;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloFuncionario;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloFuncionario;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCliente;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCondutor;
using LocadoraDeVeiculos.Core.Dominio.ModuloCondutor;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloTaxaServico;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAluguel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloDevolucao;
using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloConfiguracao;
using LocadoraDeVeiculos.Core.Dominio.ModuloConfiguracao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloParceiro;
using LocadoraDeVeiculos.Core.Dominio.ModuloParceiro;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCupom;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloDesafioCupom;
using LocadoraDeVeiculos.Core.Dominio.ModuloDesafioCupom;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;

namespace LocadoraDeVeiculos.Core.Aplicacao;

public static class DependencyInjection
{
    public static IServiceCollection AddCamadaAplicacao(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly);

        var licenseKey = configuration["LUCKYPENNY_LICENSE_KEY"];

        if (string.IsNullOrWhiteSpace(licenseKey))
            throw new Exception("A variável LUCKYPENNY_LICENSE_KEY não foi fornecida.");

        services.AddMediatR(config =>
        {
            config.LicenseKey = licenseKey;
            config.RegisterServicesFromAssembly(assembly);
        });

        services.AddScoped<IRepositorioGrupoAutomovel, RepositorioGrupoAutomovelEmOrm>();
        services.AddScoped<IRepositorioFuncionario, RepositorioFuncionarioEmOrm>();
        services.AddScoped<IRepositorioPlanoCobranca, RepositorioPlanoCobrancaEmOrm>();
        services.AddScoped<IRepositorioAutomovel, RepositorioAutomovelEmOrm>();
        services.AddScoped<IRepositorioCliente, RepositorioClienteEmOrm>();
        services.AddScoped<IRepositorioCondutor, RepositorioCondutorEmOrm>();
        services.AddScoped<IRepositorioTaxaServico, RepositorioTaxaServicoEmOrm>();
        services.AddScoped<IRepositorioAluguel, RepositorioAluguelEmOrm>();
        services.AddScoped<IRepositorioDevolucao, RepositorioDevolucaoEmOrm>();
        services.AddScoped<IRepositorioConfiguracao, RepositorioConfiguracaoEmOrm>();
        services.AddScoped<IRepositorioParceiro, RepositorioParceiroEmOrm>();
        services.AddScoped<IRepositorioCupom, RepositorioCupomEmOrm>();
        services.AddScoped<IRepositorioDesafioCupom, RepositorioDesafioCupomEmOrm>();

        // Serviço Python de ML (reconhecimento facial, chatbot): roda localmente, sem HTTPS/certificado
        // válido em desenvolvimento. Timeout curto porque essas chamadas acontecem durante o login,
        // ninguém deveria ficar esperando o reconhecimento facial por mais que alguns segundos.
        var mlApiBaseUrl = configuration["MlApi:BaseUrl"] ?? "http://localhost:8000";
        services.AddHttpClient("MlApi", client =>
        {
            client.BaseAddress = new Uri(mlApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<FacialAuthClient>();

        return services;
    }
}
