# Política de privacidade — dado biométrico (rosto)

**Versão do texto:** `2026-10-v1` · **Aplicação:** LocadoraDeVeiculos (projeto acadêmico — TCC)

> Este é um projeto educacional, sem fins comerciais. Esta página documenta como o rosto é tratado e é o texto que o sistema
> mostra ao usuário antes do cadastro facial (a fonte do texto exibido é `ConsentimentoBiometrico.cs`). Não substitui
> aconselhamento jurídico.

## 1. O que é coletado
Ao cadastrar o rosto, a câmera captura uma imagem e o sistema extrai dela uma **representação matemática** (468 pontos de
referência do rosto, *landmarks*). **A fotografia não é guardada.** A representação é a única coisa que fica.

## 2. Finalidade
Somente confirmar a identidade da pessoa no **login por reconhecimento facial** e na **verificação facial**. Não há outro uso,
não há compartilhamento com terceiros e não há venda.

## 3. Base legal
Consentimento específico e destacado do titular (LGPD, art. 11, I). O cadastro do rosto só é aceito se o usuário marcar o aceite
**da versão atual** do termo; o servidor registra quando e qual versão foi aceita (`ConsentimentosFaciais`). O consentimento é
voluntário: recusar não impede o uso do sistema com e-mail e senha.

## 4. Como é protegido
- Cifrado em repouso com AES-256-GCM; a chave fica fora do banco (`FACE_ENCRYPTION_KEYS`) e é versionada para rotação.
- O serviço de reconhecimento (ML) **não guarda nada**: recebe a imagem e as amostras só durante a conferência.
- Nenhum dado biométrico vai para logs, para a trilha de auditoria (que guarda só nomes e identificadores) nem para o repositório.
- Acesso por papel; cada pessoa só enxerga e apaga o próprio rosto.

## 5. Por quanto tempo
- **12 meses sem uso.** Cada login ou verificação bem-sucedida zera a contagem. Passado o prazo, uma rotina diária apaga o rosto
  e o consentimento automaticamente.
- **Quando a conta é encerrada** (a empresa encerra a conta, ou o funcionário é desligado): o rosto e o consentimento são
  apagados na hora, e a conta fica bloqueada para sempre.
- **Quando o titular pede:** "Apagar meu rosto" na tela de verificação facial revoga o consentimento e apaga tudo.

Dados de apoio: o IP registrado na trilha de auditoria é anonimizado após 180 dias; IP e *User-Agent* de sessões encerradas
são apagados 30 dias depois.

## 6. Direitos do titular
Confirmação e acesso (a tela mostra se há rosto cadastrado), eliminação (apagar o rosto), revogação do consentimento (o mesmo
botão) e informação sobre o tratamento (esta página).

## 7. Controlador
O controle dos dados é do próprio projeto acadêmico (TCC), que **não oferece garantias nem assume responsabilidades de
serviço comercial**. Como qualquer projeto que trata dados pessoais, ele continua sujeito à LGPD: esta declaração descreve a
natureza do projeto e não afasta direitos do titular.
