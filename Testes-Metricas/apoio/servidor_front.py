"""Serve o build de PRODUÇÃO do Angular e faz proxy de /api para a Web API, na mesma origem (como o nginx faria).

Por que não `ng serve`: o Lighthouse e os testes de ponta a ponta precisam medir o pacote que iria para produção
(minificado, com chunks lazy e cache longo), e o cookie de renovação da sessão (HttpOnly, SameSite=Lax) só vale se
o front e a API forem a mesma origem para o navegador. Só biblioteca padrão.
"""
import gzip
import http.client
import mimetypes
import re
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit

TEXTUAIS = ('text/', 'application/javascript', 'application/json', 'image/svg+xml', 'application/xml')
HOP_A_HOP = {'connection', 'keep-alive', 'proxy-authenticate', 'proxy-authorization', 'te', 'trailers', 'transfer-encoding',
             'upgrade', 'content-length', 'content-encoding'}
ARQUIVO_COM_HASH = re.compile(r'-[A-Z0-9]{8}\.(js|css)$')


class ServidorFront:
    def __init__(self, pasta: Path, porta: int, api_host: str, api_porta: int):
        self.pasta, self.porta, self.api_host, self.api_porta = pasta.resolve(), porta, api_host, api_porta
        self._http = None
        self._thread = None
        self.requisicoes = 0

    def iniciar(self) -> None:
        servidor = self

        class Manipulador(BaseHTTPRequestHandler):
            protocol_version = 'HTTP/1.1'

            def log_message(self, *args):  # silencioso: o relatório não precisa do log de acesso
                pass

            def do_GET(self):
                self._tratar()

            def do_HEAD(self):
                self._tratar()

            def do_POST(self):
                self._tratar()

            def do_PUT(self):
                self._tratar()

            def do_DELETE(self):
                self._tratar()

            def do_PATCH(self):
                self._tratar()

            def _tratar(self):
                servidor.requisicoes += 1
                caminho = urlsplit(self.path).path
                if caminho.startswith('/api/'):
                    self._proxy()
                else:
                    self._estatico(caminho)

            def _estatico(self, caminho: str):
                alvo = (servidor.pasta / caminho.lstrip('/')).resolve()
                dentro = servidor.pasta in alvo.parents or alvo == servidor.pasta
                if not dentro or not alvo.is_file():
                    # rota do Angular (/home, /alugueis/novo...): devolve o index.html
                    alvo = servidor.pasta / 'index.html'
                corpo = alvo.read_bytes()
                tipo = mimetypes.guess_type(alvo.name)[0] or 'application/octet-stream'
                if alvo.suffix == '.js':
                    tipo = 'text/javascript'
                cabecalhos = {'Content-Type': tipo + ('; charset=utf-8' if tipo.startswith('text/') else '')}
                cabecalhos['Cache-Control'] = ('public, max-age=31536000, immutable' if ARQUIVO_COM_HASH.search(alvo.name)
                                               else 'no-cache')
                if tipo.startswith(TEXTUAIS) and 'gzip' in self.headers.get('Accept-Encoding', ''):
                    corpo = gzip.compress(corpo, 6)
                    cabecalhos['Content-Encoding'] = 'gzip'
                cabecalhos['Vary'] = 'Accept-Encoding'
                self._responder(200, cabecalhos, corpo)

            def _proxy(self):
                tamanho = int(self.headers.get('Content-Length') or 0)
                corpo = self.rfile.read(tamanho) if tamanho else None
                cabecalhos = {k: v for k, v in self.headers.items() if k.lower() not in HOP_A_HOP | {'host', 'accept-encoding', 'x-metricas-ip'}}
                # O limite de autenticação da API é por IP (login, cadastro e renovação de token, que acontece a cada
                # recarga de página). Os testes recarregam muito e este proxy faz todo mundo parecer 127.0.0.1; então um teste
                # pode pedir um IP próprio com X-Metricas-Ip (como a bateria de segurança faz). Só existe neste servidor de teste.
                cabecalhos['X-Forwarded-For'] = self.headers.get('X-Metricas-Ip') or self.client_address[0]
                cabecalhos['X-Forwarded-Host'] = self.headers.get('Host', '')
                try:
                    con = http.client.HTTPConnection(servidor.api_host, servidor.api_porta, timeout=60)
                    con.request(self.command, self.path, body=corpo, headers=cabecalhos)
                    resp = con.getresponse()
                    dados = resp.read()
                    saida = [(k, v) for k, v in resp.getheaders() if k.lower() not in HOP_A_HOP]
                    con.close()
                except OSError as erro:
                    self._responder(502, {'Content-Type': 'text/plain'}, f'Web API indisponível: {erro}'.encode())
                    return
                self.send_response(resp.status)
                for k, v in saida:
                    self.send_header(k, v)
                self.send_header('Content-Length', str(len(dados)))
                self.end_headers()
                if self.command != 'HEAD':
                    self.wfile.write(dados)

            def _responder(self, status, cabecalhos, corpo):
                self.send_response(status)
                for k, v in cabecalhos.items():
                    self.send_header(k, v)
                self.send_header('Content-Length', str(len(corpo)))
                self.end_headers()
                if self.command != 'HEAD':
                    self.wfile.write(corpo)

        class Silencioso(ThreadingHTTPServer):
            def handle_error(self, request, client_address):  # o navegador aborta conexões o tempo todo; não é erro
                pass

        self._http = Silencioso(('127.0.0.1', self.porta), Manipulador)
        self._http.daemon_threads = True
        self._thread = threading.Thread(target=self._http.serve_forever, daemon=True)
        self._thread.start()

    def parar(self) -> None:
        if self._http:
            self._http.shutdown()
            self._http.server_close()

    @property
    def url(self) -> str:
        return f'http://localhost:{self.porta}'
