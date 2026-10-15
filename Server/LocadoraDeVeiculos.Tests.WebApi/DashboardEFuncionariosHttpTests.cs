using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace LocadoraDeVeiculos.Tests.WebApi;

/// <summary>
/// O painel (números da operação, só da própria empresa) e o ciclo do funcionário: cadastro cria um acesso que funciona de verdade
/// (login com o cargo certo, dados da empresa certa, sem acesso à gestão) e a exclusão encerra esse acesso.
/// </summary>
[TestClass]
[TestCategory("Web API - Painel e funcionários")]
public sealed class DashboardEFuncionariosHttpTests
{
    private static ApiFactory _fabrica = null!;

    [ClassInitialize]
    public static void Iniciar(TestContext _) => _fabrica = new ApiFactory(new() { ["RATE_LIMIT_PER_MINUTE"] = "100000" });

    [ClassCleanup]
    public static void Encerrar() => _fabrica.Dispose();

    private static Task<EmpresaDeTeste> NovaEmpresa(string cargo = "Empresa") => EmpresaDeTeste.CriarAsync(_fabrica, cargo);

    // ---------------------------------------------------------------- painel -----------------------------------------------------

    [TestMethod]
    public async Task Painel_SemDados_ZeraTudo()
    {
        using var empresa = await NovaEmpresa();

        var resumo = await empresa.GetAsync("/api/dashboard/resumo");

        Assert.AreEqual(0, resumo.Propriedade("frota").GetProperty("total").GetInt32());
        Assert.AreEqual(0, resumo.Propriedade("totalClientes").GetInt32());
        Assert.AreEqual(0, resumo.Propriedade("alugueis").GetProperty("emAndamento").GetInt32());
        Assert.AreEqual(0m, resumo.Propriedade("financeiro").GetProperty("receitaDoMes").GetDecimal());
        Assert.AreEqual(6, resumo.Propriedade("ultimosMeses").GetArrayLength(), "o gráfico sempre mostra os 6 últimos meses, mesmo vazios");
    }

    [TestMethod]
    public async Task Painel_ContaFrotaAlugueisClientesEReceita_SoDaPropriaEmpresa()
    {
        using var empresa = await NovaEmpresa();
        using var outra = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync(carros: 3);
        await outra.MontarCenarioAsync(carros: 5);                                   // não pode entrar na conta da primeira

        var reservado = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[0])).Id;
        var andamento = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[1])).Id;
        await empresa.IniciarAluguelAsync(andamento);
        var concluido = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[2])).Id;
        await empresa.IniciarAluguelAsync(concluido);
        await empresa.RegistrarDevolucaoAsync(concluido, DateTimeOffset.UtcNow);       // 3 diárias de R$ 100, sem multa nem combustível
        Assert.AreNotEqual(Guid.Empty, reservado);

        var resumo = await empresa.GetAsync("/api/dashboard/resumo");

        var frota = resumo.Propriedade("frota");
        Assert.AreEqual(3, frota.GetProperty("total").GetInt32(), "só os 3 carros desta empresa");
        Assert.AreEqual(1, frota.GetProperty("emUso").GetInt32(), "um aluguel em andamento (o concluído já devolveu o carro)");
        Assert.AreEqual(2, frota.GetProperty("disponiveis").GetInt32());
        var alugueis = resumo.Propriedade("alugueis");
        Assert.AreEqual(1, alugueis.GetProperty("emAndamento").GetInt32());
        Assert.AreEqual(1, alugueis.GetProperty("reservados").GetInt32());
        Assert.AreEqual(0, alugueis.GetProperty("atrasados").GetInt32());
        Assert.AreEqual(1, resumo.Propriedade("totalClientes").GetInt32());
        var financeiro = resumo.Propriedade("financeiro");
        Assert.AreEqual(300m, financeiro.GetProperty("receitaDoMes").GetDecimal());
        Assert.AreEqual(1, financeiro.GetProperty("devolucoesDoMes").GetInt32());
        var ultimoMes = resumo.Propriedade("ultimosMeses").EnumerateArray().Last();
        Assert.AreEqual(300m, ultimoMes.GetProperty("receita").GetDecimal(), "a receita do mês corrente aparece no último ponto do gráfico");
        Assert.AreEqual(DateTime.UtcNow.Month, ultimoMes.GetProperty("mes").GetInt32());
    }

    [TestMethod]
    public async Task Painel_AluguelEmAndamentoComPrazoVencido_ContaComoAtrasado()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync(carros: 2);
        var emDia = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[0])).Id;
        var vencido = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[1])).Id;
        await empresa.IniciarAluguelAsync(emDia);
        await empresa.IniciarAluguelAsync(vencido);
        using (var escopo = _fabrica.Services.CreateScope())
        {
            var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDeVeiculosDbContext>();
            var aluguel = await banco.Alugueis.IgnoreQueryFilters().SingleAsync(a => a.Id == vencido);
            aluguel.DataRetornoPrevisto = DateTimeOffset.UtcNow.AddDays(-2);        // o prazo passou e o carro não voltou
            await banco.SaveChangesAsync();
        }

        var resumo = await empresa.GetAsync("/api/dashboard/resumo");

        Assert.AreEqual(2, resumo.Propriedade("alugueis").GetProperty("emAndamento").GetInt32());
        Assert.AreEqual(1, resumo.Propriedade("alugueis").GetProperty("atrasados").GetInt32(), "só o de prazo vencido");
    }

    // ---------------------------------------------------------------- funcionários ------------------------------------------------

    private static object CorpoFuncionario(string email, string? cpf = null, string senha = "Senha@12345", string? confirmar = null, string nome = "Pedro Atendente") => new
    {
        nomeCompleto = nome, cpf = cpf ?? EmpresaDeTeste.Cpf(), email, senha, confirmarSenha = confirmar ?? senha, salario = 3200m, admissaoEmUtc = DateTimeOffset.UtcNow.AddMonths(-2)
    };

    private static string Email() => $"func-{Guid.NewGuid():N}@teste.com";

    private static async Task<HttpResponseMessage> LoginAsync(string email, string senha)
    {
        // IP próprio por tentativa: o limite de autenticação é por IP e não queremos misturar testes
        return await _fabrica.LoginDoIpAsync($"10.{Random.Shared.Next(1, 250)}.{Random.Shared.Next(1, 250)}.{Random.Shared.Next(1, 250)}", email, senha);
    }

    [TestMethod]
    public async Task Funcionario_Cadastrado_PodeSerLidoListadoEditadoEExcluido()
    {
        using var empresa = await NovaEmpresa();
        var email = Email();
        var cpf = EmpresaDeTeste.Cpf();

        var criado = await empresa.PostAsync("/api/funcionarios", CorpoFuncionario(email, cpf));

        Assert.AreEqual(HttpStatusCode.Created, criado.Status, criado.TextoDosErros);
        var lido = await empresa.GetAsync($"/api/funcionarios/{criado.Id}");
        Assert.AreEqual("Pedro Atendente", lido.Propriedade("nomeCompleto").GetString());
        Assert.AreEqual(cpf, lido.Propriedade("cpf").GetString());
        Assert.AreEqual(email, lido.Propriedade("email").GetString());
        Assert.AreEqual(3200m, lido.Propriedade("salario").GetDecimal());
        Assert.AreEqual(1, (await empresa.GetAsync("/api/funcionarios")).Registros.Count);

        var editado = await empresa.PutAsync($"/api/funcionarios/{criado.Id}", new { nomeCompleto = "Pedro Gerente", cpf, salario = 4500m, admissaoEmUtc = DateTimeOffset.UtcNow.AddMonths(-2) });
        Assert.IsTrue(editado.Ok, editado.TextoDosErros);
        var depois = await empresa.GetAsync($"/api/funcionarios/{criado.Id}");
        Assert.AreEqual("Pedro Gerente", depois.Propriedade("nomeCompleto").GetString());
        Assert.AreEqual(4500m, depois.Propriedade("salario").GetDecimal());

        Assert.IsTrue((await empresa.DeleteAsync($"/api/funcionarios/{criado.Id}")).Ok);
        Assert.AreEqual(HttpStatusCode.NotFound, (await empresa.GetAsync($"/api/funcionarios/{criado.Id}")).Status);
    }

    [TestMethod]
    public async Task Funcionario_Criado_FazLoginComOCargoCertoEEnxergaOsDadosDaEmpresa_MasNaoAGestao()
    {
        using var empresa = await NovaEmpresa();
        var email = Email();
        await empresa.CriarClienteAsync("Cliente da Locadora");
        await empresa.PostAsync("/api/funcionarios", CorpoFuncionario(email));

        using var login = await LoginAsync(email, "Senha@12345");

        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("chave").GetString()!;
        var claims = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToList();
        Assert.IsTrue(claims.Any(c => c.Type is "role" or System.Security.Claims.ClaimTypes.Role && c.Value == "Funcionario"), "cargo Funcionario no token");
        Assert.AreEqual(empresa.EmpresaId.ToString(), claims.First(c => c.Type == "EmpresaId").Value, "o funcionário opera na empresa que o cadastrou");

        using var http = _fabrica.CriarClienteAutenticado(token);
        var clientes = await http.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/clientes");
        Assert.AreEqual(1, clientes.GetProperty("registros").GetArrayLength(), "vê os clientes da empresa");
        Assert.AreEqual(HttpStatusCode.Forbidden, (await http.GetAsync("/api/funcionarios")).StatusCode, "a gestão de funcionários é só da empresa");
        Assert.AreEqual(HttpStatusCode.Forbidden, (await http.GetAsync("/api/configuracoes")).StatusCode);
    }

    [TestMethod]
    public async Task Funcionario_Excluido_NaoConsegueMaisEntrar()
    {
        using var empresa = await NovaEmpresa();
        var email = Email();
        var funcionario = await empresa.PostAsync("/api/funcionarios", CorpoFuncionario(email));
        using (var antes = await LoginAsync(email, "Senha@12345"))
            Assert.AreEqual(HttpStatusCode.OK, antes.StatusCode, "antes de excluir, o acesso funciona");

        Assert.IsTrue((await empresa.DeleteAsync($"/api/funcionarios/{funcionario.Id}")).Ok);

        using var depois = await LoginAsync(email, "Senha@12345");
        Assert.AreNotEqual(HttpStatusCode.OK, depois.StatusCode, "o acesso do funcionário excluído precisa ser encerrado");
    }

    [TestMethod]
    public async Task Funcionario_Editar_ComSenhaNova_TrocaAsenhaDeAcesso()
    {
        using var empresa = await NovaEmpresa();
        var email = Email();
        var cpf = EmpresaDeTeste.Cpf();
        var criado = await empresa.PostAsync("/api/funcionarios", CorpoFuncionario(email, cpf));

        var troca = await empresa.PutAsync($"/api/funcionarios/{criado.Id}",
            new { nomeCompleto = "Pedro Atendente", cpf, salario = 3200m, admissaoEmUtc = DateTimeOffset.UtcNow.AddMonths(-2), senha = "NovaSenha@456" });

        Assert.IsTrue(troca.Ok, troca.TextoDosErros);
        using var comNova = await LoginAsync(email, "NovaSenha@456");
        using var comAntiga = await LoginAsync(email, "Senha@12345");
        Assert.AreEqual(HttpStatusCode.OK, comNova.StatusCode);
        Assert.AreNotEqual(HttpStatusCode.OK, comAntiga.StatusCode, "a senha antiga não vale mais");
    }

    [TestMethod]
    public async Task Funcionario_ComCpfRepetido_Recebe409_ComEmailRepetidoOuSenhaFraca_Recebe400_ENadaFicaGravado()
    {
        using var empresa = await NovaEmpresa();
        var email = Email();
        var cpf = EmpresaDeTeste.Cpf();
        await empresa.PostAsync("/api/funcionarios", CorpoFuncionario(email, cpf));

        var mesmoCpf = await empresa.PostAsync("/api/funcionarios", CorpoFuncionario(Email(), cpf));
        var mesmoEmail = await empresa.PostAsync("/api/funcionarios", CorpoFuncionario(email));
        var senhaFraca = await empresa.PostAsync("/api/funcionarios", CorpoFuncionario(Email(), senha: "abc"));
        var confirmacaoErrada = await empresa.PostAsync("/api/funcionarios", CorpoFuncionario(Email(), senha: "Senha@12345", confirmar: "Outra@12345"));

        Assert.AreEqual(HttpStatusCode.Conflict, mesmoCpf.Status);
        StringAssert.Contains(mesmoCpf.TextoDosErros, "Um funcionário com este CPF já está cadastrado");
        Assert.AreEqual(HttpStatusCode.BadRequest, mesmoEmail.Status, "o e-mail é o login e precisa ser único");
        Assert.AreEqual(HttpStatusCode.BadRequest, senhaFraca.Status);
        StringAssert.Contains(senhaFraca.TextoDosErros, "pelo menos 8 caracteres");
        Assert.AreEqual(HttpStatusCode.BadRequest, confirmacaoErrada.Status);
        StringAssert.Contains(confirmacaoErrada.TextoDosErros, "As senhas não conferem");
        Assert.AreEqual(1, (await empresa.GetAsync("/api/funcionarios")).Registros.Count);
    }

    [TestMethod]
    public async Task Funcionario_DeOutraEmpresa_EInvisivel_ECpfPodeRepetirEmOutraEmpresa()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var cpf = EmpresaDeTeste.Cpf();
        var funcionario = (await dona.PostAsync("/api/funcionarios", CorpoFuncionario(Email(), cpf))).Id;

        var mesmoCpfEmOutra = await intrusa.PostAsync("/api/funcionarios", CorpoFuncionario(Email(), cpf));

        Assert.AreEqual(HttpStatusCode.Created, mesmoCpfEmOutra.Status, "o CPF só precisa ser único dentro da empresa: " + mesmoCpfEmOutra.TextoDosErros);
        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.GetAsync($"/api/funcionarios/{funcionario}")).Status);
        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.DeleteAsync($"/api/funcionarios/{funcionario}")).Status);
        Assert.AreEqual(1, (await intrusa.GetAsync("/api/funcionarios")).Registros.Count, "só o dela");
        Assert.AreEqual(1, (await dona.GetAsync("/api/funcionarios")).Registros.Count);
    }
}
