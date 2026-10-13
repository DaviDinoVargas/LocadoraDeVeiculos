# Reconhecimento facial por embedding (S33)

Problema: o comparador anterior olhava só a **geometria dos 468 pontos** do MediaPipe, com limiares nunca medidos. No LFW (protocolo oficial de 6000 pares) ele **recusava a pessoa certa em 94 % dos casos e ainda aceitava 1,8 % de impostores**; o ponto de equilíbrio (EER) ficava perto de 45 %. A geometria sozinha não separa pessoas.

## O que foi feito

- **Modelo:** SFace, do *OpenCV Zoo* (`github.com/opencv/opencv_zoo`), licença **Apache-2.0**. É uma rede MobileFaceNet treinada com a perda SFace. Transforma um rosto alinhado em um **vetor de 128 números**; duas fotos da mesma pessoa dão vetores com cosseno alto.
- **Alinhamento:** os 5 pontos clássicos (olhos, nariz, cantos da boca) saem dos landmarks do MediaPipe que o ML já extrai. Nenhum detector extra foi baixado.
- **Decisão:** cosseno entre o vetor da foto e os vetores cadastrados (basta bater com um). Limiar padrão **0,363** (`FACE_COSINE_THRESHOLD`), o recomendado pelo OpenCV e confirmado pela medição abaixo.
- **Armazenamento:** continua cifrado (AES-256-GCM) na tabela `AmostrasFaciais`, mas agora cada amostra tem **128 números** em vez de 1404. Amostras no formato antigo são ignoradas na leitura e apagadas quando a pessoa recadastra o rosto. **Quem já tinha rosto cadastrado precisa cadastrar de novo** (a foto não é guardada, então não há como converter).
- **Falha fechada:** sem o arquivo do modelo (ou com hash diferente) o módulo facial responde 503; nunca cai para o método fraco.
- **Texto de consentimento:** versão `2026-10-v2` (descreve a lista de 128 números); quem recadastra aceita de novo.
- **Removido:** o importador do antigo `faces.json` (`--importar-faces`), que só trazia a geometria antiga.

## De onde vem o arquivo (e por que é confiável)

| Item | Fonte | Verificação |
|---|---|---|
| `face_recognition_sface_2021dec.onnx` (38.696.353 bytes) | `media.githubusercontent.com/media/opencv/opencv_zoo/47534e27c9851bb1128ccc0102f1145e27f23f98/models/face_recognition_sface/` (HTTPS, commit fixo) | SHA-256 `0ba9fbfa…4e79` e tamanho conferem com o **ponteiro Git LFS do próprio repositório**; conferidos no download e de novo antes de carregar |

O arquivo não vai para o git (`*.onnx` não é versionado; `baixar_sface.py` baixa e confere). Para baixar: `py FaceAuth/baixar_sface.py`. Não instala nada no sistema e não executa nada: o OpenCV só lê o modelo ONNX.

## Resultado medido (LFW, 6000 pares oficiais, 10 dobras)

| Medida | Geometria (antes) | **SFace (agora)** |
|---|---|---|
| Acurácia pelo protocolo (limiar escolhido nas outras 9 dobras) | ≈ 57 % (no melhor limiar) | **98,3 %** (±0,6) |
| Ponto de equilíbrio (EER) | ≈ 43–46 % | **≈ 2,6 %** |
| Pessoa certa recusada (FRR) no limiar usado | 94 % | **3,1 %** (limiar 0,363) |
| Pessoa errada aceita (FAR) no limiar usado | 1,8 % | **0,067 %** (1 em ~1500) |
| Limiar para FAR de 1 % / 0,1 % | — | 0,30 / 0,355 |

Cosseno médio: **0,67** para a mesma pessoa e **0,09** para pessoas diferentes. Cada rosto leva cerca de 20 ms. Uma foto do LFW ficou sem rosto detectável e foi excluída.

Observações honestas:

1. O OpenCV Zoo relata ~99,4 % com o detector e o alinhamento dele; aqui deu 98,3 % porque o alinhamento sai dos landmarks do MediaPipe e as fotos do LFW são pequenas (250 px). Com webcam e boa luz deve ser igual ou melhor, mas **não foi medido com a câmera real**.
2. Mesmo com FAR de 0,067 %, o login facial continua protegido por bloqueio de conta (10 erros), limite por conta + IP (5), prova de vida (S3) e, se ativo, o código do 2FA.
3. O vetor também é **dado biométrico**: segue cifrado, com consentimento, retenção de 12 meses e exclusão ao encerrar a conta.

Reproduzir: `py -I tools/medir_embedding_lfw.py <pasta-do-lfw> <pasta-de-saida>` (em `MachineLeaning/`); só números vão para a saída (fora do repositório).

## Variáveis

| Variável | Onde | Padrão |
|---|---|---|
| `FACE_MODEL_DIR` | ML | `MachineLeaning/FaceAuth/modelos` |
| `FACE_COSINE_THRESHOLD` | ML | `0.363` |
