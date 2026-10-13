# Prova de vida no login facial (S3)

Objetivo: impedir que uma **foto impressa ou a imagem na tela de um celular** do rosto cadastrado entre no sistema.

## O que foi feito

- **Modelo:** dois MiniFASNet (`MiniFASNetV2`, escala 2,7× e `MiniFASNetV1SE`, escala 4×) do projeto *Silent-Face-Anti-Spoofing*, da Minivision, licença **Apache-2.0**. Cada um é uma rede minúscula (~0,4 milhão de parâmetros, ~1,8 MB) que olha o recorte do rosto, com contexto ao redor, e dá a probabilidade de ser um rosto real. As duas respostas são somadas, como no projeto original.
- **Onde atua:** no módulo facial do ML (`/face/landmarks` e `/face/compare`). Cadastro e login com foto de foto/tela são recusados; nesse caso o `match` é sempre `false`, mesmo que a geometria do rosto bata.
- **Na Web API (.NET):** `FacialAuthClient` lê `provaDeVidaAtiva`/`vivo`. Foto reprovada no login conta como **falha de tentativa** (bloqueio de conta e limite por conta+IP) e fica registrada no log; no cadastro mostra a mensagem "a imagem não parece ser de uma pessoa de verdade". **Fora de Development a prova de vida é obrigatória**: se o ML responder sem tê-la feito (versão antiga ou desligada), a foto é recusada (`ANTISPOOF_REQUIRED` força true/false).
- **Falha fechada no ML:** com `ANTISPOOF_ENABLED` ligado (o padrão) e sem os pesos, as rotas respondem **503**, nunca liberam.

## De onde vêm os arquivos (e por que são confiáveis)

| Item | Fonte | Verificação |
|---|---|---|
| Pesos `2.7_80x80_MiniFASNetV2.pth` e `4_0_0_80x80_MiniFASNetV1SE.pth` | `raw.githubusercontent.com/minivision-ai/Silent-Face-Anti-Spoofing` no **commit fixo** `b6d5f04ad78778917853b25c778acef6d5626d15` (HTTPS) | SHA-256 no código (`antispoof/liveness.py`) conferido no download **e** antes de carregar; o hash git (blob) dos arquivos bate com o que a API do GitHub informa para o commit; carregados com `torch.load(weights_only=True)` (só tensores, nunca código) |
| Arquitetura `minifasnet.py` | mesmo commit | copiada sem alterações, com cabeçalho de atribuição e a licença Apache em `antispoof/LICENSE-Silent-Face-Anti-Spoofing`; o SHA-256 do original está no cabeçalho; o arquivo só importa `torch` (sem rede, `os` ou execução) |
| Fotos de teste (LFW) | espelho `marcelohaps/lfw` no Hugging Face, revisão fixa `12a61458…`, com os 6000 pares oficiais (10 dobras) | não há hash oficial para este espelho (o figshare recusou o download); o primeiro par confere com o LFW original; o conjunto serve só para medir, nunca é executado nem entra no repositório (fica em `A:\TCC\Downloads-Modelos\lfw`) |

Os pesos não vão para o git (`*.pth` ignorado). Para baixar: `py FaceAuth/antispoof/baixar_modelo.py` (grava só se o hash conferir). Não instala nada no sistema.

## Números medidos (LFW, 7.700 fotos de pessoas reais e 6000 pares)

| Medida | Resultado |
|---|---|
| Fotos reais recusadas pela prova de vida (limiar 0,5) | **≈ 6,6 %** (4,4 % no limiar 0,3); mediana da pontuação 0,99 |
| Comparador de identidade atual (limiar 0,02 / 0,9): pessoa errada aceita (FAR) | **1,8 %** |
| Comparador de identidade atual: pessoa certa recusada (FRR) | **94 %** |
| Ponto de equilíbrio (EER) na melhor combinação testada | **≈ 43–46 %** |

Leitura:

1. **A prova de vida deixa passar quase todas as fotos reais.** Os ~7 % recusados são fotos da internet (compressão, óculos escuros, ângulo); com webcam e boa luz a taxa deve ser menor, mas não foi medida.
2. **A capacidade de pegar ataques NÃO foi medida:** o LFW não tem fotos impressas nem de tela, e não existe base pública que possa ser baixada sem cadastro/licença. É preciso um teste com a webcam: segurar uma foto impressa e uma foto na tela do celular e ver a pontuação (o projeto original também avisa que a robustez varia conforme a câmera).
3. **Achado novo e mais grave: o comparador de identidade é fraco.** Ele compara só a geometria dos 468 pontos do MediaPipe, centralizada no centroide. No LFW (poses e expressões variadas) ele erra o rosto certo em 94 % e ainda aceita 1,8 % de impostores: **a geometria sozinha não separa pessoas** (EER perto do acaso). Na webcam, com a mesma pessoa, na mesma pose e luz, funciona melhor, mas não é uma base segura de autenticação. A recomendação é trocar por um **modelo de embedding facial** (ArcFace/MobileFaceNet) e medir de novo com o mesmo script; isso depende de baixar outro modelo (nova decisão do usuário).

Reproduzir: `py -I tools/medir_face_lfw.py <pasta-do-lfw> <pasta-de-saida>` (em `MachineLeaning/`); os resultados em JSON vão para fora do repositório.

## Como testar com a webcam (opcional, feito pelo usuário)

Com o ML rodando, enviar quadros reais ao `/face/landmarks` (ou usar a tela de cadastro do rosto) e observar `pontuacaoDeVida`: rosto de verdade deve ficar perto de 1; foto impressa ou na tela, perto de 0. Se rostos verdadeiros forem recusados, reduzir `ANTISPOOF_THRESHOLD` (padrão 0,5).

## Variáveis

| Variável | Onde | Padrão |
|---|---|---|
| `ANTISPOOF_ENABLED` | ML | `true` (desligar só em desenvolvimento) |
| `ANTISPOOF_THRESHOLD` | ML | `0.5` |
| `ANTISPOOF_DIR` | ML | `MachineLeaning/FaceAuth/modelos` |
| `ANTISPOOF_REQUIRED` | Web API | obrigatória fora de Development |
