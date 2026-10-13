namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;

/// <summary>
/// Termo de consentimento para o uso do rosto (LGPD, art. 11, I: consentimento específico e destacado). A API entrega este
/// texto ao front, que o mostra antes do cadastro do rosto; o cadastro só vale se a versão aceita for a atual. Ao mudar
/// o texto, troque <see cref="VersaoAtual"/>: quem aceitou a versão anterior precisa aceitar de novo para recadastrar.
/// O mesmo texto está em docs/POLITICA_DE_PRIVACIDADE_BIOMETRIA.md.
/// </summary>
public static class ConsentimentoBiometrico
{
    public const string VersaoAtual = "2026-10-v2";

    public const int RetencaoEmMeses = 12;

    public const string Texto = """
        TERMO DE CONSENTIMENTO PARA USO DO ROSTO (DADO BIOMÉTRICO)

        O que é coletado. Ao cadastrar o seu rosto, o sistema extrai da imagem da sua câmera uma representação matemática do rosto (uma lista de 128 números calculada por um modelo de reconhecimento facial). A fotografia NÃO é guardada.

        Para que serve. Somente para confirmar a sua identidade no login por reconhecimento facial e na verificação facial. Não é usado para nenhuma outra finalidade, não é compartilhado com terceiros e não é vendido.

        Como é protegido. A representação do rosto é guardada cifrada, em banco de dados, e só é lida pelo servidor da aplicação no momento de conferir o seu rosto.

        Por quanto tempo. Por até 12 meses SEM USO: se você não entrar com o rosto durante 12 meses, ele é apagado automaticamente. Também é apagado quando a sua conta é encerrada ou quando você pede.

        Seus direitos. Você pode apagar o seu rosto a qualquer momento na tela de verificação facial ("Apagar meu rosto"); isso revoga este consentimento e o login por rosto deixa de funcionar até um novo cadastro. A revogação não afeta o login com e-mail e senha. Você também pode pedir informações sobre os seus dados.

        Quem controla os dados. Este sistema é um projeto acadêmico (Trabalho de Conclusão de Curso), de uso educacional e sem fins comerciais. O controle dos dados é do próprio projeto, que não oferece garantias nem assume responsabilidades de serviço comercial.

        O consentimento é voluntário: não aceitar não impede o uso do sistema com e-mail e senha.
        """;
}
