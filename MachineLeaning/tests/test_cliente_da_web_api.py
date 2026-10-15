"""O chatbot consulta a Web API em nome do usuário logado: repassa o token dele, traduz as falhas em mensagens que o chat pode mostrar e
aceita os dois jeitos de a API nomear a lista (`registros`/`Registros`). Aqui a Web API é falsa (httpx.MockTransport)."""
import asyncio
import os
import sys

import httpx
import pytest

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from ChatBot import dotnet_client as cliente


def _com_transporte(monkeypatch, responder):
    """Troca o httpx.AsyncClient do módulo por um que fala com uma Web API falsa; devolve a lista de requisições recebidas."""
    recebidas = []

    def manipulador(requisicao: httpx.Request) -> httpx.Response:
        recebidas.append(requisicao)
        return responder(requisicao)

    classe_real = httpx.AsyncClient
    monkeypatch.setattr(cliente.httpx, 'AsyncClient',
                        lambda **kw: classe_real(transport=httpx.MockTransport(manipulador), base_url=kw.get('base_url', 'http://api'),
                                                 timeout=kw.get('timeout')))
    return recebidas


def test_repassa_o_token_do_usuario_como_bearer_e_devolve_o_json(monkeypatch):
    recebidas = _com_transporte(monkeypatch, lambda r: httpx.Response(200, json={'ok': True}))

    assert asyncio.run(cliente._get('/api/qualquer', 'abc.def.ghi')) == {'ok': True}

    assert recebidas[0].headers['Authorization'] == 'Bearer abc.def.ghi'
    assert recebidas[0].url.path == '/api/qualquer'


def test_sem_token_nao_manda_cabecalho_de_autorizacao(monkeypatch):
    recebidas = _com_transporte(monkeypatch, lambda r: httpx.Response(200, json={}))

    asyncio.run(cliente._get('/api/x', None))

    assert 'Authorization' not in recebidas[0].headers


def test_sessao_expirada_vira_erro_401_com_mensagem_para_o_usuario(monkeypatch):
    _com_transporte(monkeypatch, lambda r: httpx.Response(401))

    with pytest.raises(cliente.DotnetApiError) as erro:
        asyncio.run(cliente._get('/api/x', 'token-vencido'))

    assert erro.value.status_code == 401
    assert 'faça login novamente' in erro.value.detail


@pytest.mark.parametrize('status', [403, 404, 500])
def test_outros_erros_da_api_mantem_o_status_e_dizem_qual_rota_falhou(monkeypatch, status):
    _com_transporte(monkeypatch, lambda r: httpx.Response(status))

    with pytest.raises(cliente.DotnetApiError) as erro:
        asyncio.run(cliente._get('/api/automoveis', 't'))

    assert erro.value.status_code == status
    assert '/api/automoveis' in erro.value.detail and str(status) in erro.value.detail


def test_web_api_fora_do_ar_vira_503_sem_expor_o_erro_de_rede(monkeypatch):
    def cair(requisicao):
        raise httpx.ConnectError('Connection refused 10.0.0.5:7064')

    _com_transporte(monkeypatch, cair)

    with pytest.raises(cliente.DotnetApiError) as erro:
        asyncio.run(cliente._get('/api/x', 't'))

    assert erro.value.status_code == 503
    assert 'fora do ar' in erro.value.detail
    assert '10.0.0.5' not in erro.value.detail, 'o endereço interno não vaza para a conversa'


@pytest.mark.parametrize('chave', ['registros', 'Registros'])
def test_listas_aceitam_os_dois_nomes_de_propriedade_da_api(monkeypatch, chave):
    _com_transporte(monkeypatch, lambda r: httpx.Response(200, json={chave: [{'id': 1}, {'id': 2}]}))

    assert asyncio.run(cliente.listar_automoveis('t')) == [{'id': 1}, {'id': 2}]
    assert asyncio.run(cliente.listar_alugueis_em_aberto('t')) == [{'id': 1}, {'id': 2}]
    assert asyncio.run(cliente.listar_cupons('t')) == [{'id': 1}, {'id': 2}]


def test_lista_sem_a_propriedade_vira_lista_vazia_e_cada_consulta_vai_para_a_rota_certa(monkeypatch):
    recebidas = _com_transporte(monkeypatch, lambda r: httpx.Response(200, json={'resumo': 1}))

    assert asyncio.run(cliente.listar_automoveis('t')) == []
    assert asyncio.run(cliente.obter_resumo_dashboard('t')) == {'resumo': 1}
    asyncio.run(cliente.listar_alugueis_em_aberto('t'))
    asyncio.run(cliente.listar_cupons('t'))

    assert [r.url.path for r in recebidas] == ['/api/automoveis', '/api/dashboard/resumo', '/api/alugueis/em-aberto', '/api/cupons']
