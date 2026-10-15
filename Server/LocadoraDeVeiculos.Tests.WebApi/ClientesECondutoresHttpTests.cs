using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using System.Net;

namespace LocadoraDeVeiculos.Tests.WebApi;

/// <summary>
/// Clientes (pessoa física e jurídica) e condutores pela API de verdade: cadastro, regras de duplicidade, validações,
/// bloqueio com aluguel em aberto e isolamento entre empresas. Cada teste usa uma empresa própria.
/// </summary>
[TestClass]
[TestCategory("Web API - Clientes e condutores")]
public sealed class ClientesECondutoresHttpTests
{
    private static ApiFactory _fabrica = null!;

    [ClassInitialize]
    public static void Iniciar(TestContext _) => _fabrica = new ApiFactory(new() { ["RATE_LIMIT_PER_MINUTE"] = "100000" });

    [ClassCleanup]
    public static void Encerrar() => _fabrica.Dispose();

    private static Task<EmpresaDeTeste> NovaEmpresa(string cargo = "Empresa") => EmpresaDeTeste.CriarAsync(_fabrica, cargo);

    private static object CorpoClientePf(string? cpf = null, string? nome = null, Guid? pessoaJuridicaId = null, DateTime? validadeCnh = null,
        string telefone = "(11) 99999-0000", string email = "cliente@teste.com") => new
    {
        nome = nome ?? "Maria Souza", cpf = cpf ?? EmpresaDeTeste.Cpf(), rg = "12.345.678-9", cnh = EmpresaDeTeste.Cnh(),
        validadeCnh = validadeCnh ?? DateTime.UtcNow.AddYears(2), telefone, email, endereco = "Rua dos Testes, 100", clientePessoaJuridicaId = pessoaJuridicaId
    };

    private static object CorpoClientePj(string? cnpj = null, string nome = "Transportes Exemplo Ltda") => new
    {
        nome, cnpj = cnpj ?? EmpresaDeTeste.Cnpj(), nomeFantasia = "Exemplo Transportes", telefone = "(11) 3333-4444", email = "contato@exemplo.com",
        endereco = "Avenida Central, 500"
    };

    // ---------------------------------------------------------------- cliente pessoa física ---------------------------------

    [TestMethod]
    public async Task ClientePf_Criado_AparecePorIdNaListaGeralENaListaDePessoasFisicas()
    {
        using var empresa = await NovaEmpresa();
        var cpf = EmpresaDeTeste.Cpf();

        var criado = await empresa.PostAsync("/api/clientes/pessoa-fisica", CorpoClientePf(cpf, "Maria Souza"));

        Assert.AreEqual(HttpStatusCode.Created, criado.Status, criado.TextoDosErros);
        var porId = await empresa.GetAsync($"/api/clientes/{criado.Id}");
        Assert.AreEqual("Maria Souza", porId.Propriedade("nome").GetString());
        Assert.AreEqual(cpf, porId.Propriedade("cpf").GetString());
        var geral = await empresa.GetAsync("/api/clientes");
        Assert.AreEqual(1, geral.Registros.Count);
        Assert.AreEqual(cpf, geral.Registros[0].GetProperty("documentoPrincipal").GetString());
        Assert.AreEqual(1, (await empresa.GetAsync("/api/clientes/pessoas-fisicas")).Registros.Count);
        Assert.AreEqual(0, (await empresa.GetAsync("/api/clientes/pessoas-juridicas")).Registros.Count);
    }

    [TestMethod]
    public async Task ClientePf_ComCpfRepetido_Recebe409_MasOutraEmpresaPodeCadastrarOMesmoCpf()
    {
        using var empresaA = await NovaEmpresa();
        using var empresaB = await NovaEmpresa();
        var cpf = EmpresaDeTeste.Cpf();
        await empresaA.PostAsync("/api/clientes/pessoa-fisica", CorpoClientePf(cpf));

        var repetido = await empresaA.PostAsync("/api/clientes/pessoa-fisica", CorpoClientePf(cpf));
        var daOutra = await empresaB.PostAsync("/api/clientes/pessoa-fisica", CorpoClientePf(cpf));

        Assert.AreEqual(HttpStatusCode.Conflict, repetido.Status);
        StringAssert.Contains(repetido.TextoDosErros, "Já existe um cliente com este CPF");
        Assert.AreEqual(HttpStatusCode.Created, daOutra.Status);
        Assert.AreEqual(1, (await empresaA.GetAsync("/api/clientes")).Registros.Count);
    }

    [TestMethod]
    [DataRow("111.111.111-11", "CPF só com dígitos repetidos")]
    [DataRow("123.456.789-00", "dígitos verificadores errados")]
    [DataRow("12345678900", "sem a máscara")]
    public async Task ClientePf_ComCpfInvalido_Recebe400(string cpf, string motivo)
    {
        using var empresa = await NovaEmpresa();

        var resposta = await empresa.PostAsync("/api/clientes/pessoa-fisica", CorpoClientePf(cpf));

        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.Status, motivo);
        StringAssert.Contains(resposta.TextoDosErros, "Cpf", motivo);
        Assert.AreEqual(0, (await empresa.GetAsync("/api/clientes")).Registros.Count);
    }

    [TestMethod]
    public async Task ClientePf_ComCnhVencida_TelefoneForaDoPadraoOuEmailInvalido_Recebe400ComCadaMensagem()
    {
        using var empresa = await NovaEmpresa();

        var vencida = await empresa.PostAsync("/api/clientes/pessoa-fisica", CorpoClientePf(validadeCnh: DateTime.UtcNow.AddDays(-1)));
        var telefone = await empresa.PostAsync("/api/clientes/pessoa-fisica", CorpoClientePf(telefone: "11999990000"));
        var email = await empresa.PostAsync("/api/clientes/pessoa-fisica", CorpoClientePf(email: "isto-nao-e-email"));

        StringAssert.Contains(vencida.TextoDosErros, "validade futura");
        StringAssert.Contains(telefone.TextoDosErros, "Telefone");
        StringAssert.Contains(email.TextoDosErros, "e-mail válido");
        Assert.AreEqual(0, (await empresa.GetAsync("/api/clientes")).Registros.Count, "nenhum cadastro inválido pode ter sido gravado");
    }

    [TestMethod]
    public async Task ClientePf_Editar_AceitaManterOCpf_MasNaoTomarOdeOutroCliente()
    {
        using var empresa = await NovaEmpresa();
        var cpfA = EmpresaDeTeste.Cpf();
        var a = (await empresa.PostAsync("/api/clientes/pessoa-fisica", CorpoClientePf(cpfA, "Cliente A"))).Id;
        var cpfB = EmpresaDeTeste.Cpf();
        await empresa.PostAsync("/api/clientes/pessoa-fisica", CorpoClientePf(cpfB, "Cliente B"));

        var manter = await empresa.PutAsync($"/api/clientes/pessoa-fisica/{a}", CorpoClientePf(cpfA, "Cliente A Renomeado"));
        var tomar = await empresa.PutAsync($"/api/clientes/pessoa-fisica/{a}", CorpoClientePf(cpfB, "Cliente A Renomeado"));

        Assert.IsTrue(manter.Ok, manter.TextoDosErros);
        Assert.AreEqual(HttpStatusCode.Conflict, tomar.Status);
        var atual = await empresa.GetAsync($"/api/clientes/{a}");
        Assert.AreEqual("Cliente A Renomeado", atual.Propriedade("nome").GetString());
        Assert.AreEqual(cpfA, atual.Propriedade("cpf").GetString(), "a edição recusada não pode trocar o CPF");
    }

    [TestMethod]
    public async Task Cliente_ComAluguelEmAberto_NaoPodeSerEditadoNemExcluido_ESemAluguelPodeSerExcluido()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync();
        Assert.AreEqual(HttpStatusCode.Created, (await empresa.PostAsync("/api/alugueis", EmpresaDeTeste.CorpoDeAluguel(cenario))).Status);
        var livre = (await empresa.CriarClienteAsync("Cliente Livre")).Id;

        var edicao = await empresa.PutAsync($"/api/clientes/pessoa-fisica/{cenario.Cliente}", CorpoClientePf());
        var exclusaoBloqueada = await empresa.DeleteAsync($"/api/clientes/{cenario.Cliente}");
        var exclusaoLivre = await empresa.DeleteAsync($"/api/clientes/{livre}");

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, edicao.Status);
        StringAssert.Contains(edicao.TextoDosErros, "aluguel em aberto");
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, exclusaoBloqueada.Status);
        Assert.AreEqual(HttpStatusCode.OK, (await empresa.GetAsync($"/api/clientes/{cenario.Cliente}")).Status, "o cliente com aluguel continua cadastrado");
        Assert.IsTrue(exclusaoLivre.Ok, exclusaoLivre.TextoDosErros);
        Assert.AreEqual(HttpStatusCode.NotFound, (await empresa.GetAsync($"/api/clientes/{livre}")).Status);
    }

    [TestMethod]
    public async Task Cliente_DeOutraEmpresa_EInvisivel_NaoPodeSerLidoEditadoNemExcluido()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var cliente = (await dona.PostAsync("/api/clientes/pessoa-fisica", CorpoClientePf(nome: "Só da dona"))).Id;

        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.GetAsync($"/api/clientes/{cliente}")).Status);
        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.PutAsync($"/api/clientes/pessoa-fisica/{cliente}", CorpoClientePf())).Status);
        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.DeleteAsync($"/api/clientes/{cliente}")).Status);
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/clientes")).Registros.Count);
        Assert.AreEqual("Só da dona", (await dona.GetAsync($"/api/clientes/{cliente}")).Propriedade("nome").GetString());
    }

    [TestMethod]
    public async Task ClientePf_NaoPodeSerVinculadoAPessoaJuridicaDeOutraEmpresa()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var pjDaDona = (await dona.PostAsync("/api/clientes/pessoa-juridica", CorpoClientePj())).Id;

        var resposta = await intrusa.PostAsync("/api/clientes/pessoa-fisica", CorpoClientePf(pessoaJuridicaId: pjDaDona));

        Assert.IsFalse(resposta.Ok, "o vínculo com a empresa-cliente de outra locadora não pode ser aceito");
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/clientes")).Registros.Count);
    }

    [TestMethod]
    public async Task ClientePf_NaoPodeSerReligadoAPessoaJuridicaDeOutraEmpresaPelaEdicao()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var pjDaDona = (await dona.PostAsync("/api/clientes/pessoa-juridica", CorpoClientePj())).Id;
        var cpf = EmpresaDeTeste.Cpf();
        var pf = (await intrusa.PostAsync("/api/clientes/pessoa-fisica", CorpoClientePf(cpf))).Id;

        var edicao = await intrusa.PutAsync($"/api/clientes/pessoa-fisica/{pf}", CorpoClientePf(cpf, pessoaJuridicaId: pjDaDona));

        Assert.IsFalse(edicao.Ok);
        Assert.AreEqual(System.Text.Json.JsonValueKind.Null, (await intrusa.GetAsync($"/api/clientes/{pf}")).Propriedade("clientePessoaJuridicaId").ValueKind,
            "a edição recusada não pode ter criado o vínculo");
    }

    [TestMethod]
    public async Task ClientePf_VinculadoAPessoaJuridicaDaPropriaEmpresa_MostraONomeDaEmpresa()
    {
        using var empresa = await NovaEmpresa();
        var pj = (await empresa.PostAsync("/api/clientes/pessoa-juridica", CorpoClientePj(nome: "Frota Exemplo SA"))).Id;

        var pf = await empresa.PostAsync("/api/clientes/pessoa-fisica", CorpoClientePf(pessoaJuridicaId: pj));

        Assert.AreEqual(HttpStatusCode.Created, pf.Status, pf.TextoDosErros);
        var lido = await empresa.GetAsync($"/api/clientes/{pf.Id}");
        Assert.AreEqual(pj, lido.Propriedade("clientePessoaJuridicaId").GetGuid());
        Assert.AreEqual("Frota Exemplo SA", lido.Propriedade("clientePessoaJuridicaNome").GetString());
    }

    // ---------------------------------------------------------------- cliente pessoa jurídica -------------------------------

    [TestMethod]
    public async Task ClientePj_Criado_AparecePorIdENaListaDePessoasJuridicas()
    {
        using var empresa = await NovaEmpresa();
        var cnpj = EmpresaDeTeste.Cnpj();

        var criado = await empresa.PostAsync("/api/clientes/pessoa-juridica", CorpoClientePj(cnpj));

        Assert.AreEqual(HttpStatusCode.Created, criado.Status, criado.TextoDosErros);
        var porId = await empresa.GetAsync($"/api/clientes/{criado.Id}");
        Assert.AreEqual(cnpj, porId.Propriedade("cnpj").GetString());
        Assert.AreEqual("Exemplo Transportes", porId.Propriedade("nomeFantasia").GetString());
        Assert.AreEqual(1, (await empresa.GetAsync("/api/clientes/pessoas-juridicas")).Registros.Count);
    }

    [TestMethod]
    public async Task ClientePj_ComCnpjRepetidoOuInvalido_EhRecusado()
    {
        using var empresa = await NovaEmpresa();
        var cnpj = EmpresaDeTeste.Cnpj();
        await empresa.PostAsync("/api/clientes/pessoa-juridica", CorpoClientePj(cnpj));

        var repetido = await empresa.PostAsync("/api/clientes/pessoa-juridica", CorpoClientePj(cnpj));
        var invalido = await empresa.PostAsync("/api/clientes/pessoa-juridica", CorpoClientePj("11.111.111/1111-11"));

        Assert.AreEqual(HttpStatusCode.Conflict, repetido.Status);
        StringAssert.Contains(repetido.TextoDosErros, "Já existe um cliente com este CNPJ");
        Assert.AreEqual(HttpStatusCode.BadRequest, invalido.Status);
        Assert.AreEqual(1, (await empresa.GetAsync("/api/clientes/pessoas-juridicas")).Registros.Count);
    }

    [TestMethod]
    public async Task ClientePj_Editar_TrocaOsDadosEMantemOCnpj_MasNaoTomaOdeOutra()
    {
        using var empresa = await NovaEmpresa();
        var cnpjA = EmpresaDeTeste.Cnpj();
        var a = (await empresa.PostAsync("/api/clientes/pessoa-juridica", CorpoClientePj(cnpjA, "Empresa A Ltda"))).Id;
        var cnpjB = EmpresaDeTeste.Cnpj();
        await empresa.PostAsync("/api/clientes/pessoa-juridica", CorpoClientePj(cnpjB, "Empresa B Ltda"));

        var renomear = await empresa.PutAsync($"/api/clientes/pessoa-juridica/{a}", CorpoClientePj(cnpjA, "Empresa A Renomeada"));
        var tomar = await empresa.PutAsync($"/api/clientes/pessoa-juridica/{a}", CorpoClientePj(cnpjB, "Empresa A Renomeada"));

        Assert.IsTrue(renomear.Ok, renomear.TextoDosErros);
        Assert.AreEqual(HttpStatusCode.Conflict, tomar.Status);
        Assert.AreEqual(cnpjA, (await empresa.GetAsync($"/api/clientes/{a}")).Propriedade("cnpj").GetString());
    }

    // ---------------------------------------------------------------- condutores --------------------------------------------

    [TestMethod]
    public async Task Condutor_Criado_AparecePorIdNaListaEPorCliente_ComONomeDoCliente()
    {
        using var empresa = await NovaEmpresa();
        var cliente = (await empresa.CriarClienteAsync("Cliente Titular")).Id;
        var outro = (await empresa.CriarClienteAsync("Outro Cliente")).Id;

        var criado = await empresa.CriarCondutorAsync(cliente);
        await empresa.CriarCondutorAsync(outro);

        Assert.AreEqual(HttpStatusCode.Created, criado.Status, criado.TextoDosErros);
        var porId = await empresa.GetAsync($"/api/condutores/{criado.Id}");
        Assert.AreEqual(cliente, porId.Propriedade("clienteId").GetGuid());
        Assert.AreEqual("Cliente Titular", porId.Propriedade("clienteNome").GetString());
        Assert.AreEqual(2, (await empresa.GetAsync("/api/condutores")).Registros.Count);
        var doCliente = await empresa.GetAsync($"/api/condutores/cliente/{cliente}");
        CollectionAssert.AreEqual(new[] { criado.Id }, doCliente.Registros.Select(r => r.GetProperty("id").GetGuid()).ToArray());
    }

    [TestMethod]
    public async Task Condutor_ComCpfOuCnhRepetidos_Recebe409_ComAMensagemDoCampoCerto()
    {
        using var empresa = await NovaEmpresa();
        var cliente = (await empresa.CriarClienteAsync()).Id;
        var cpf = EmpresaDeTeste.Cpf();
        var cnh = EmpresaDeTeste.Cnh();
        await empresa.CriarCondutorAsync(cliente, cpf: cpf, cnh: cnh);

        var mesmoCpf = await empresa.CriarCondutorAsync(cliente, cpf: cpf);
        var mesmaCnh = await empresa.CriarCondutorAsync(cliente, cnh: cnh);

        Assert.AreEqual(HttpStatusCode.Conflict, mesmoCpf.Status);
        StringAssert.Contains(mesmoCpf.TextoDosErros, "condutor com este CPF");
        Assert.AreEqual(HttpStatusCode.Conflict, mesmaCnh.Status);
        StringAssert.Contains(mesmaCnh.TextoDosErros, "condutor com esta CNH");
        Assert.AreEqual(1, (await empresa.GetAsync("/api/condutores")).Registros.Count);
    }

    [TestMethod]
    public async Task Condutor_ComCnhVencida_Recebe400()
    {
        using var empresa = await NovaEmpresa();
        var cliente = (await empresa.CriarClienteAsync()).Id;

        var resposta = await empresa.CriarCondutorAsync(cliente, validadeCnh: DateTime.UtcNow.AddDays(-30));

        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.Status);
        StringAssert.Contains(resposta.TextoDosErros, "validade futura");
        Assert.AreEqual(0, (await empresa.GetAsync("/api/condutores")).Registros.Count);
    }

    [TestMethod]
    public async Task Condutor_NaoPodeSerVinculadoAClienteDeOutraEmpresa()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var clienteDaDona = (await dona.CriarClienteAsync()).Id;

        var resposta = await intrusa.CriarCondutorAsync(clienteDaDona);

        Assert.IsFalse(resposta.Ok, "o condutor ficaria ligado a um cliente que a empresa não enxerga");
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/condutores")).Registros.Count);
    }

    [TestMethod]
    public async Task Condutor_NaoPodeSerReligadoAClienteDeOutraEmpresaPelaEdicao()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var clienteDaDona = (await dona.CriarClienteAsync()).Id;
        var clienteDaIntrusa = (await intrusa.CriarClienteAsync()).Id;
        var cpf = EmpresaDeTeste.Cpf();
        var cnh = EmpresaDeTeste.Cnh();
        var condutor = (await intrusa.CriarCondutorAsync(clienteDaIntrusa, cpf: cpf, cnh: cnh)).Id;

        var edicao = await intrusa.PutAsync($"/api/condutores/{condutor}", new
        {
            nome = "Condutor de Teste", email = "condutor@teste.com", cpf, cnh, validadeCnh = DateTime.UtcNow.AddYears(2),
            telefone = "(11) 98888-0000", clienteId = clienteDaDona
        });

        Assert.IsFalse(edicao.Ok);
        Assert.AreEqual(clienteDaIntrusa, (await intrusa.GetAsync($"/api/condutores/{condutor}")).Propriedade("clienteId").GetGuid());
    }

    [TestMethod]
    public async Task Condutor_Editar_AceitaManterDocumentos_MasNaoTomarOsDeOutroCondutor()
    {
        using var empresa = await NovaEmpresa();
        var cliente = (await empresa.CriarClienteAsync()).Id;
        var cpfA = EmpresaDeTeste.Cpf();
        var cnhA = EmpresaDeTeste.Cnh();
        var a = (await empresa.CriarCondutorAsync(cliente, cpf: cpfA, cnh: cnhA)).Id;
        var cpfB = EmpresaDeTeste.Cpf();
        await empresa.CriarCondutorAsync(cliente, cpf: cpfB);

        object Corpo(string cpf, string cnh, string nome) => new
        {
            nome, email = "condutor@teste.com", cpf, cnh, validadeCnh = DateTime.UtcNow.AddYears(2), telefone = "(11) 98888-0000", clienteId = cliente
        };

        var renomear = await empresa.PutAsync($"/api/condutores/{a}", Corpo(cpfA, cnhA, "Nome Novo"));
        var tomarCpf = await empresa.PutAsync($"/api/condutores/{a}", Corpo(cpfB, cnhA, "Nome Novo"));

        Assert.IsTrue(renomear.Ok, renomear.TextoDosErros);
        Assert.AreEqual("Nome Novo", (await empresa.GetAsync($"/api/condutores/{a}")).Propriedade("nome").GetString());
        Assert.AreEqual(HttpStatusCode.Conflict, tomarCpf.Status);
        Assert.AreEqual(cpfA, (await empresa.GetAsync($"/api/condutores/{a}")).Propriedade("cpf").GetString());
    }

    [TestMethod]
    public async Task Condutor_ComAluguelEmAberto_NaoPodeSerEditadoNemExcluido()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync();
        Assert.AreEqual(HttpStatusCode.Created, (await empresa.PostAsync("/api/alugueis", EmpresaDeTeste.CorpoDeAluguel(cenario))).Status);

        var edicao = await empresa.PutAsync($"/api/condutores/{cenario.Condutor}", new
        {
            nome = "Outro Nome", email = "condutor@teste.com", cpf = EmpresaDeTeste.Cpf(), cnh = EmpresaDeTeste.Cnh(),
            validadeCnh = DateTime.UtcNow.AddYears(2), telefone = "(11) 98888-0000", clienteId = cenario.Cliente
        });
        var exclusao = await empresa.DeleteAsync($"/api/condutores/{cenario.Condutor}");

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, edicao.Status);
        StringAssert.Contains(edicao.TextoDosErros, "aluguel em aberto");
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, exclusao.Status);
        Assert.AreEqual("Condutor de Teste", (await empresa.GetAsync($"/api/condutores/{cenario.Condutor}")).Propriedade("nome").GetString());
    }

    [TestMethod]
    public async Task Condutor_SemAluguel_PodeSerExcluido_EDeOutraEmpresaEInvisivel()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var cliente = (await dona.CriarClienteAsync()).Id;
        var condutor = (await dona.CriarCondutorAsync(cliente)).Id;
        var descartavel = (await dona.CriarCondutorAsync(cliente)).Id;

        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.GetAsync($"/api/condutores/{condutor}")).Status);
        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.DeleteAsync($"/api/condutores/{condutor}")).Status);
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/condutores")).Registros.Count);
        Assert.IsTrue((await dona.DeleteAsync($"/api/condutores/{descartavel}")).Ok);
        Assert.AreEqual(HttpStatusCode.NotFound, (await dona.GetAsync($"/api/condutores/{descartavel}")).Status);
        Assert.AreEqual(1, (await dona.GetAsync("/api/condutores")).Registros.Count);
    }

    // ---------------------------------------------------------------- papéis ------------------------------------------------

    [TestMethod]
    public async Task Funcionario_PodeCadastrarClientesECondutores_DaEmpresaDele()
    {
        using var dona = await NovaEmpresa();
        using var funcionario = await EmpresaDeTeste.DoMesmoTenantAsync(_fabrica, dona, "Funcionario");

        var cliente = await funcionario.CriarClienteAsync("Cliente do Balcão");
        var condutor = await funcionario.CriarCondutorAsync(cliente.Id);

        Assert.AreEqual(HttpStatusCode.Created, cliente.Status, cliente.TextoDosErros);
        Assert.AreEqual(HttpStatusCode.Created, condutor.Status, condutor.TextoDosErros);
        Assert.AreEqual(1, (await dona.GetAsync("/api/clientes")).Registros.Count, "o que o funcionário cadastra é da empresa, e a dona vê");
    }
}
