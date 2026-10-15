"""SSRF (o servidor fazendo requisições em nome do atacante): um "canário" (um servidor local que só conta conexões) é plantado como URL em
todos os campos de texto livre que a API aceita, inclusive a foto do veículo, e depois todas as telas que mostram esses dados são consultadas.
Se o servidor (Web API ou ML) um dia resolver uma URL de entrada, buscar uma imagem, validar um link ou gerar uma miniatura, o canário recebe a
conexão e a verificação reprova. A conexão de controle (feita pelo próprio teste) prova que o canário funciona."""
import datetime
import random
import socket
import string
import threading
import time

from lib import ambiente
from lib.http import id_de as _id, ip_unico, requisitar

from .h_negocio import _cnh, _iso, _placa

CAT = 'SSRF'


class Canario:
    """Servidor TCP de uma linha: aceita conexão, lê o que chegar e conta. Não responde nada útil."""

    def __init__(self):
        self.soquete = socket.socket()
        self.soquete.bind(('127.0.0.1', 0))
        self.soquete.listen(50)
        self.soquete.settimeout(0.5)
        self.porta = self.soquete.getsockname()[1]
        self.conexoes = []
        self._parar = threading.Event()
        self._fio = threading.Thread(target=self._rodar, daemon=True)
        self._fio.start()

    def _rodar(self):
        while not self._parar.is_set():
            try:
                cliente, origem = self.soquete.accept()
            except (socket.timeout, OSError):
                continue
            try:
                cliente.settimeout(0.3)
                try:
                    primeiros = cliente.recv(200)
                except OSError:
                    primeiros = b''
                self.conexoes.append((time.time(), origem, primeiros[:60]))
            finally:
                cliente.close()

    def url(self, caminho='x', esquema='http'):
        return f'{esquema}://127.0.0.1:{self.porta}/{caminho}'

    def parar(self):
        self._parar.set()
        self._fio.join(timeout=2)
        self.soquete.close()


def executar(ctx):
    r, base = ctx.reg, ctx.base
    canario = Canario()
    try:
        # controle: o canário enxerga uma conexão de verdade (senão "zero conexões" não provaria nada)
        controle = requisitar('GET', canario.url('controle'), timeout=3)
        time.sleep(0.5)
        r.checar('L00', CAT, 'Controle: o canário registra uma conexão feita pelo próprio teste', '1 conexão', f'{len(canario.conexoes)} conexões',
                 len(canario.conexoes) == 1, 'baixa')
        canario.conexoes.clear()
        _plantar_e_consultar(ctx, canario)
        time.sleep(4)   # tempo para qualquer busca assíncrona ou em segundo plano acontecer
        r.checar('L01', CAT, 'Nenhuma URL plantada em campos de texto (foto, descrições, nomes, endereços) faz o servidor abrir conexão', '0 conexões no canário',
                 f'{len(canario.conexoes)} conexões: {[c[2] for c in canario.conexoes][:3]}', not canario.conexoes, 'alta', 'S31')
    finally:
        canario.parar()


def _plantar_e_consultar(ctx, canario):
    r, base = ctx.reg, ctx.base
    emp = ambiente.nova_empresa(base, 'ssrf')
    ctx.lembrar(emp.senha, emp.token)
    t = emp.token

    def post(caminho, corpo):
        return requisitar('POST', f'{base}{caminho}', token=t, ip=ip_unico(), json_corpo=corpo)

    u = canario.url
    validade = _iso(datetime.datetime.now() + datetime.timedelta(days=900))
    aceitos = {}

    grupo = post('/api/grupos-automovel', {'nome': 'Grupo ' + u('g'), 'descricao': u('grupo-descricao')})
    grupo_id = _id(post('/api/grupos-automovel', {'nome': 'Grupo SSRF ' + ''.join(random.choices(string.ascii_uppercase, k=5)), 'descricao': u('grupo-descricao')}))
    aceitos['grupo'] = grupo.status
    if grupo_id:
        auto = post('/api/automoveis', {'placa': _placa(), 'marca': u('marca'), 'cor': u('cor'), 'modelo': u('modelo'), 'tipoCombustivel': 'Flex',
                                       'capacidadeTanque': 50, 'ano': 2022, 'foto': canario.url('foto.jpg', 'https'), 'grupoAutomovelId': grupo_id})
        aceitos['automóvel com foto https'] = auto.status
        for esquema in ('http', 'ftp', 'gopher', 'file'):
            x = post('/api/automoveis', {'placa': _placa(), 'marca': 'Marca', 'cor': 'Preto', 'modelo': 'Modelo', 'tipoCombustivel': 'Flex',
                                        'capacidadeTanque': 50, 'ano': 2022, 'foto': f'{esquema}://127.0.0.1:{canario.porta}/foto.jpg',
                                        'grupoAutomovelId': grupo_id})
            aceitos[f'foto {esquema}'] = x.status
    cliente = post('/api/clientes/pessoa-fisica', {'nome': 'Cliente SSRF', 'cpf': ambiente.cpf_valido(), 'rg': '123456789', 'cnh': _cnh(), 'validadeCnh': validade,
                                                  'telefone': '(11) 99999-0000', 'email': 'ssrf@teste.local', 'endereco': u('endereco')})
    aceitos['cliente'] = cliente.status
    post('/api/clientes/pessoa-juridica', {'nome': 'Empresa ' + u('pj'), 'cnpj': ambiente.cnpj_valido(), 'nomeFantasia': u('fantasia'),
                                           'telefone': '(11) 3333-4444', 'email': 'pj@teste.local', 'endereco': u('endereco-pj')})
    post('/api/parceiros', {'nome': 'Parceiro ' + u('parceiro'), 'cnpj': ambiente.cnpj_valido(), 'categoria': u('categoria')})
    post('/api/cupons', {'codigo': 'SSRF' + ''.join(random.choices(string.ascii_uppercase, k=5)), 'descricao': u('cupom'), 'tipoDesconto': 'Percentual',
                         'valorDesconto': 10, 'validoAte': _iso(datetime.datetime.now() + datetime.timedelta(days=30)), 'limiteUsos': 3, 'parceiroId': None})
    post('/api/taxas-servicos', {'nome': 'Taxa ' + u('taxa'), 'preco': 10, 'tipoCalculo': 'Fixo'})
    senha = ambiente.senha_forte()
    ctx.lembrar(senha)
    post('/api/funcionarios', {'nomeCompleto': 'Func ' + u('func'), 'cpf': ambiente.cpf_valido(), 'email': 'func-ssrf@teste.local', 'senha': senha,
                               'confirmarSenha': senha, 'salario': 3000,
                               'admissaoEmUtc': _iso(datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(days=10))})
    requisitar('POST', f'{base}/api/auth/registrar', ip=ip_unico(), json_corpo={
        'nomeCompleto': 'Conta ' + u('conta'), 'email': f'ssrf-{ambiente.secrets.token_hex(3)}@teste.local', 'senha': senha, 'confirmarSenha': senha})

    # tudo que mostra esses dados de volta (uma busca em cada tela), mais a trilha de auditoria e o painel
    for caminho in ('/api/grupos-automovel', '/api/automoveis', '/api/automoveis/disponiveis', '/api/clientes', '/api/clientes/pessoas-fisicas',
                    '/api/clientes/pessoas-juridicas', '/api/parceiros', '/api/cupons', '/api/taxas-servicos', '/api/funcionarios',
                    '/api/dashboard/resumo', '/api/auditoria', '/api/auth/sessao'):
        requisitar('GET', f'{base}{caminho}', token=t, ip=ip_unico())

    foto_https = aceitos.get('automóvel com foto https')
    r.info('L02', CAT, 'Como a API respondeu aos cadastros com URL (a foto https legítima é aceita; os outros esquemas, recusados)',
           {k: v for k, v in aceitos.items() if k.startswith('foto') or 'foto' in k}, 'S31')
    if ctx.ml:
        # o chat é o único ponto em que o ML age em nome do usuário: uma URL na pergunta também não pode virar uma busca
        dono = ctx.dona_das_cameras
        if dono:
            requisitar('POST', f'{ctx.ml}/chat', token=dono.token, ip=ip_unico(), json_corpo={'message': f'veja {u("chat")} e {u("chat2", "https")}'})
