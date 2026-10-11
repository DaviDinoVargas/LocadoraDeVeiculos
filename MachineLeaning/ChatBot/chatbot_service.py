# Chatbot baseado em regras/busca (intenção por palavra-chave + regex),
# não em um modelo de linguagem treinado. A decisão de não usar um LLM
# próprio foi deliberada: não existe dataset nem checkpoint treinado
# neste projeto, e treinar um do zero não é viável no prazo do TCC nem
# necessário — as perguntas de um sistema de locadora (disponibilidade de
# veículo, status de aluguel por placa, etc.) têm um conjunto pequeno e
# previsível de intenções, então buscar a resposta certa na Web API já
# existente é mais robusto do que gerar texto livre.
import re

from . import dotnet_client
from .dotnet_client import DotnetApiError

PADRAO_PLACA = re.compile(r'\b([A-Za-z]{3}[-\s]?\d[A-Za-z0-9]\d{2})\b')

AJUDA = (
    "Posso ajudar com informações rápidas da locadora. Exemplos do que "
    "perguntar:\n"
    "- \"quantos veículos estão disponíveis?\"\n"
    "- \"qual o status da placa ABC1234?\"\n"
    "- \"quantos aluguéis estão em aberto?\"\n"
    "- \"quantos aluguéis estão atrasados?\"\n"
    "- \"quantos clientes temos?\"\n"
    "- \"quantas devoluções neste mês?\"\n"
    "- \"qual o faturamento do mês?\"\n"
    "- \"quais cupons estão ativos?\""
)


async def _tratar_consulta_placa(placa: str, token):
    alugueis = await dotnet_client.listar_alugueis_em_aberto(token)
    placa_normalizada = re.sub(r'[-\s]', '', placa).upper()
    for aluguel in alugueis:
        placa_aluguel = re.sub(r'[-\s]', '', aluguel.get('automovelPlaca', '')).upper()
        if placa_aluguel == placa_normalizada:
            devolucao = aluguel.get('dataRetornoPrevisto', 'sem data informada')
            return (
                f"A placa {placa.upper()} está em um aluguel em aberto "
                f"(cliente: {aluguel.get('clienteNome', 'não informado')}, "
                f"status: {aluguel.get('status', 'desconhecido')}, "
                f"devolução prevista: {devolucao})."
            )

    automoveis = await dotnet_client.listar_automoveis(token)
    for automovel in automoveis:
        placa_automovel = re.sub(r'[-\s]', '', automovel.get('placa', '')).upper()
        if placa_automovel == placa_normalizada:
            return (
                f"A placa {placa.upper()} pertence a um {automovel.get('marca', '')} "
                f"{automovel.get('modelo', '')} e não está em nenhum aluguel em aberto "
                "no momento — disponível para locação."
            )

    return f"Não encontrei nenhum veículo cadastrado com a placa {placa.upper()}."


async def _tratar_disponibilidade(token):
    automoveis = await dotnet_client.listar_automoveis(token)
    alugueis_abertos = await dotnet_client.listar_alugueis_em_aberto(token)
    placas_alugadas = {
        re.sub(r'[-\s]', '', a.get('automovelPlaca', '')).upper() for a in alugueis_abertos
    }
    total = len(automoveis)
    disponiveis = [
        a for a in automoveis
        if re.sub(r'[-\s]', '', a.get('placa', '')).upper() not in placas_alugadas
    ]
    if total == 0:
        return "Ainda não há veículos cadastrados no sistema."
    return (
        f"{len(disponiveis)} de {total} veículo(s) cadastrado(s) estão disponíveis "
        f"agora (os outros {total - len(disponiveis)} têm aluguel em aberto)."
    )


async def _tratar_alugueis_em_aberto(token):
    alugueis = await dotnet_client.listar_alugueis_em_aberto(token)
    if not alugueis:
        return "Não há nenhum aluguel em aberto no momento."
    linhas = [
        f"- {a.get('automovelPlaca', '?')} ({a.get('clienteNome', 'cliente não informado')}, "
        f"status {a.get('status', '?')})"
        for a in alugueis[:10]
    ]
    resumo = "\n".join(linhas)
    extra = "" if len(alugueis) <= 10 else f"\n... e mais {len(alugueis) - 10}."
    return f"Há {len(alugueis)} aluguel(éis) em aberto:\n{resumo}{extra}"


def _moeda(valor) -> str:
    inteiro, _, centavos = f"{float(valor or 0):,.2f}".partition('.')
    return f"R$ {inteiro.replace(',', '.')},{centavos}"


async def _tratar_clientes(token):
    resumo = await dotnet_client.obter_resumo_dashboard(token)
    total = resumo.get('totalClientes', 0)
    return f"Há {total} cliente(s) cadastrado(s)."


async def _tratar_atrasados(token):
    resumo = await dotnet_client.obter_resumo_dashboard(token)
    atrasados = resumo.get('alugueis', {}).get('atrasados', 0)
    if not atrasados:
        return "Nenhum aluguel está atrasado no momento."
    return f"{atrasados} aluguel(éis) em andamento já passaram da data de retorno prevista."


async def _tratar_devolucoes(token):
    resumo = await dotnet_client.obter_resumo_dashboard(token)
    financeiro = resumo.get('financeiro', {})
    qtd = financeiro.get('devolucoesDoMes', 0)
    return f"Foram registradas {qtd} devolução(ões) neste mês, somando {_moeda(financeiro.get('receitaDoMes'))}."


async def _tratar_faturamento(token):
    financeiro = (await dotnet_client.obter_resumo_dashboard(token)).get('financeiro', {})
    return (
        f"O faturamento das devoluções deste mês é {_moeda(financeiro.get('receitaDoMes'))} "
        f"(mês anterior: {_moeda(financeiro.get('receitaMesAnterior'))})."
    )


async def _tratar_cupons(token):
    cupons = [c for c in await dotnet_client.listar_cupons(token) if c.get('ativo')]
    if not cupons:
        return "Não há cupons ativos no momento."
    linhas = [
        f"- {c.get('codigo', '?')}: {c.get('descricao', '')} "
        f"({c.get('usosAtuais', 0)}/{c.get('limiteUsos') or '∞'} usos)"
        for c in cupons[:10]
    ]
    extra = "" if len(cupons) <= 10 else f"\n... e mais {len(cupons) - 10}."
    return f"Há {len(cupons)} cupom(ns) ativo(s):\n" + "\n".join(linhas) + extra


async def responder(mensagem: str, token: str | None) -> str:
    texto = (mensagem or '').strip()
    if not texto:
        return AJUDA

    texto_normalizado = texto.lower()

    try:
        match_placa = PADRAO_PLACA.search(texto)
        if match_placa and ('placa' in texto_normalizado or 'veículo' in texto_normalizado or 'veiculo' in texto_normalizado):
            return await _tratar_consulta_placa(match_placa.group(1), token)

        if 'cliente' in texto_normalizado:
            return await _tratar_clientes(token)

        if 'atrasad' in texto_normalizado:
            return await _tratar_atrasados(token)

        if 'devolu' in texto_normalizado:
            return await _tratar_devolucoes(token)

        if any(p in texto_normalizado for p in ['faturamento', 'receita', 'faturou']):
            return await _tratar_faturamento(token)

        if 'cupom' in texto_normalizado or 'cupon' in texto_normalizado:
            return await _tratar_cupons(token)

        if any(p in texto_normalizado for p in ['disponív', 'disponiv', 'livre']):
            return await _tratar_disponibilidade(token)

        if 'em aberto' in texto_normalizado or 'abertos' in texto_normalizado:
            return await _tratar_alugueis_em_aberto(token)

        if match_placa:
            return await _tratar_consulta_placa(match_placa.group(1), token)

        if any(p in texto_normalizado for p in ['oi', 'olá', 'ola', 'bom dia', 'boa tarde', 'boa noite']):
            return "Olá! " + AJUDA

        return "Não entendi bem o pedido. " + AJUDA
    except DotnetApiError as e:
        return f"Não consegui consultar o sistema agora ({e.detail})."
