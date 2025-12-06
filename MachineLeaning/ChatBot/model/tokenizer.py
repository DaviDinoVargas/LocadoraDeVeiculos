import string

class CharTokenizer:
    def __init__(self):
        # Todos caracteres que aparecem nos comandos e no JSON
        chars = (
            string.ascii_letters +     # A-Z, a-z
            string.digits +            # 0-9
            " {}\":,-T/:.()\n"         # símbolos comuns do JSON e datas
        )
        # Remove duplicatas
        chars = "".join(sorted(set(chars)))
        self.vocab = {c:i for i,c in enumerate(chars)}
        self.inv_vocab = {i:c for c,i in self.vocab.items()}
        self.vocab_size = len(self.vocab)

    def encode(self, text, seq_len=None):
        tokens = [self.vocab[c] for c in text if c in self.vocab]
        if seq_len:
            tokens = tokens + [0]*(seq_len - len(tokens)) if len(tokens)<seq_len else tokens[:seq_len]
        return tokens

    def decode(self, tokens):
        return "".join([self.inv_vocab[t] for t in tokens if t in self.inv_vocab])
