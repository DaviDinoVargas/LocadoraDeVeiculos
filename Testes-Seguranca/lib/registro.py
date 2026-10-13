"""Registro dos resultados: cada verificação diz o que era esperado, o que foi obtido e se passou."""
import json
from dataclasses import asdict, dataclass, field


@dataclass
class Verificacao:
    id: str
    categoria: str
    titulo: str
    esperado: str
    obtido: str
    passou: bool
    severidade: str = 'media'  # se falhar: alta | media | baixa
    ref: str = ''  # item do relatório de segurança (S24...)
    informativo: bool = False  # observação: não conta como aprovada nem reprovada
    pendente: bool = False  # lacuna conhecida e aceita por ora (depende de produto/serviço externo): aparece no resumo


@dataclass
class Registro:
    itens: list = field(default_factory=list)
    erros_de_script: list = field(default_factory=list)

    def checar(self, id, categoria, titulo, esperado, obtido, passou, severidade='media', ref=''):
        v = Verificacao(id, categoria, titulo, str(esperado), str(obtido), bool(passou), severidade, ref)
        self.itens.append(v)
        marca = 'OK ' if v.passou else 'FALHA'
        print(f'  [{marca}] {id} {titulo}' + ('' if v.passou else f'  (esperado: {v.esperado} | obtido: {v.obtido})'), flush=True)
        return v.passou

    def info(self, id, categoria, titulo, obtido, ref=''):
        self.itens.append(Verificacao(id, categoria, titulo, '-', str(obtido), True, 'baixa', ref, informativo=True))
        print(f'  [INFO] {id} {titulo}: {obtido}', flush=True)

    def pendente(self, id, categoria, titulo, obtido, ref=''):
        """Lacuna de segurança conhecida que hoje não tem como ser fechada só com código (e-mail transacional, decisão de
        produto...). Não reprova a execução, mas fica listada em toda rodada para não ser esquecida."""
        self.itens.append(Verificacao(id, categoria, titulo, '-', str(obtido), True, 'media', ref, informativo=True, pendente=True))
        print(f'  [PENDENTE] {id} {titulo}: {obtido}', flush=True)

    def erro(self, modulo, ex):
        self.erros_de_script.append(f'{modulo}: {type(ex).__name__}: {ex}')
        print(f'  [ERRO DE SCRIPT] {modulo}: {type(ex).__name__}: {ex}', flush=True)

    @property
    def aprovadas(self):
        return [v for v in self.itens if v.passou and not v.informativo]

    @property
    def reprovadas(self):
        return [v for v in self.itens if not v.passou]

    def para_dict(self):
        return {'verificacoes': [asdict(v) for v in self.itens], 'erros_de_script': self.erros_de_script}

    def salvar(self, caminho):
        with open(caminho, 'w', encoding='utf-8') as f:
            json.dump(self.para_dict(), f, ensure_ascii=False, indent=2)
