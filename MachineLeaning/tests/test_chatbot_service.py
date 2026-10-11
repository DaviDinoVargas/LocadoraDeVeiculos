"""Testa o roteamento de intenção do chatbot de regras/busca (ChatBot/chatbot_service.py)
sem depender da Web API .NET de verdade -- as funções que consultam a API são
substituídas (monkeypatch) por dados fake.
"""
import asyncio
import os
import sys

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from ChatBot import chatbot_service


def _run(coro):
    return asyncio.run(coro)


def test_padrao_placa_reconhece_formato_mercosul_sem_separador():
    match = chatbot_service.PADRAO_PLACA.search("a placa ABC1D23 está disponível?")
    assert match is not None
    assert match.group(1) == "ABC1D23"


def test_padrao_placa_reconhece_com_traco_e_espaco():
    assert chatbot_service.PADRAO_PLACA.search("ABC-1D23") is not None
    assert chatbot_service.PADRAO_PLACA.search("ABC 1D23") is not None


def test_responder_mensagem_vazia_retorna_ajuda():
    resposta = _run(chatbot_service.responder("", token=None))
    assert "Posso ajudar" in resposta


def test_responder_saudacao_retorna_ajuda_com_saudacao():
    resposta = _run(chatbot_service.responder("Olá, bom dia", token=None))
    assert resposta.startswith("Olá!")


def test_responder_pergunta_nao_reconhecida_retorna_ajuda():
    resposta = _run(chatbot_service.responder("qual a previsão do tempo amanhã?", token=None))
    assert "Não entendi" in resposta


def test_responder_disponibilidade_usa_dados_reais_da_api(monkeypatch):
    async def fake_listar_automoveis(token):
        return [
            {"placa": "ABC1D23"},
            {"placa": "XYZ9K87"},
        ]

    async def fake_listar_alugueis_em_aberto(token):
        return [{"automovelPlaca": "ABC1D23", "clienteNome": "Cliente Teste", "status": "EmAndamento"}]

    monkeypatch.setattr(chatbot_service.dotnet_client, "listar_automoveis", fake_listar_automoveis)
    monkeypatch.setattr(chatbot_service.dotnet_client, "listar_alugueis_em_aberto", fake_listar_alugueis_em_aberto)

    resposta = _run(chatbot_service.responder("quantos veículos estão disponíveis?", token="fake-token"))

    assert "1 de 2" in resposta


def test_responder_consulta_placa_alugada_traz_dados_do_aluguel(monkeypatch):
    async def fake_listar_alugueis_em_aberto(token):
        return [{
            "automovelPlaca": "ABC1D23",
            "clienteNome": "Maria Teste",
            "status": "EmAndamento",
            "dataRetornoPrevisto": "2026-10-05",
        }]

    monkeypatch.setattr(chatbot_service.dotnet_client, "listar_alugueis_em_aberto", fake_listar_alugueis_em_aberto)

    resposta = _run(chatbot_service.responder("qual o status da placa abc-1d23?", token="fake-token"))

    assert "Maria Teste" in resposta
    # a resposta ecoa a placa como o usuário digitou (maiúscula, com o
    # separador original) -- só a comparação internamente ignora o separador
    assert "ABC-1D23" in resposta


def test_responder_quando_api_esta_fora_do_ar_avisa_sem_quebrar(monkeypatch):
    async def fake_listar_automoveis(token):
        raise chatbot_service.DotnetApiError(503, "sistema principal (Web API) está fora do ar no momento")

    monkeypatch.setattr(chatbot_service.dotnet_client, "listar_automoveis", fake_listar_automoveis)

    resposta = _run(chatbot_service.responder("veículos disponíveis?", token="fake-token"))

    assert "não consegui" in resposta.lower() or "fora do ar" in resposta.lower()


# ---- intenções de clientes, devoluções, atrasos, faturamento e cupons (resumo do dashboard) ----

RESUMO = {
    "frota": {"total": 2, "emUso": 1, "disponiveis": 1},
    "alugueis": {"emAndamento": 1, "reservados": 0, "atrasados": 2},
    "financeiro": {"receitaDoMes": 1234.5, "receitaMesAnterior": 10, "devolucoesDoMes": 3},
    "totalClientes": 7,
}


def _fake_resumo(monkeypatch, resumo=RESUMO):
    async def fake(token):
        return resumo

    monkeypatch.setattr(chatbot_service.dotnet_client, "obter_resumo_dashboard", fake)


def test_responder_quantos_clientes(monkeypatch):
    _fake_resumo(monkeypatch)
    assert "7 cliente" in _run(chatbot_service.responder("quantos clientes temos?", token="t"))


def test_responder_alugueis_atrasados(monkeypatch):
    _fake_resumo(monkeypatch)
    assert "2 aluguel" in _run(chatbot_service.responder("tem algum aluguel atrasado?", token="t"))


def test_responder_sem_atrasados_diz_que_nao_ha(monkeypatch):
    _fake_resumo(monkeypatch, {**RESUMO, "alugueis": {"atrasados": 0}})
    assert "Nenhum aluguel" in _run(chatbot_service.responder("aluguéis atrasados", token="t"))


def test_responder_devolucoes_do_mes_formata_moeda_brasileira(monkeypatch):
    _fake_resumo(monkeypatch)
    resposta = _run(chatbot_service.responder("quantas devoluções neste mês?", token="t"))
    assert "3 devolu" in resposta and "R$ 1.234,50" in resposta


def test_responder_faturamento(monkeypatch):
    _fake_resumo(monkeypatch)
    resposta = _run(chatbot_service.responder("qual o faturamento?", token="t"))
    assert "R$ 1.234,50" in resposta and "R$ 10,00" in resposta


def test_responder_cupons_lista_so_os_ativos(monkeypatch):
    async def fake(token):
        return [
            {"codigo": "NATAL10", "descricao": "Natal", "ativo": True, "usosAtuais": 1, "limiteUsos": 5},
            {"codigo": "VELHO", "descricao": "Antigo", "ativo": False, "usosAtuais": 0, "limiteUsos": None},
        ]

    monkeypatch.setattr(chatbot_service.dotnet_client, "listar_cupons", fake)
    resposta = _run(chatbot_service.responder("quais cupons estão ativos?", token="t"))
    assert "NATAL10" in resposta and "VELHO" not in resposta and "1 cupom" in resposta


def test_placa_continua_tendo_prioridade_sobre_as_novas_intencoes(monkeypatch):
    async def fake_alugueis(token):
        return []

    async def fake_automoveis(token):
        return [{"placa": "ABC1D23", "marca": "Fiat", "modelo": "Uno"}]

    monkeypatch.setattr(chatbot_service.dotnet_client, "listar_alugueis_em_aberto", fake_alugueis)
    monkeypatch.setattr(chatbot_service.dotnet_client, "listar_automoveis", fake_automoveis)
    # "cliente" aparece na frase, mas a placa deve vencer
    resposta = _run(chatbot_service.responder("a placa ABC1D23 é de algum cliente?", token="t"))
    assert "Fiat" in resposta


def test_ajuda_lista_as_novas_perguntas():
    assert "cupons" in chatbot_service.AJUDA and "faturamento" in chatbot_service.AJUDA
