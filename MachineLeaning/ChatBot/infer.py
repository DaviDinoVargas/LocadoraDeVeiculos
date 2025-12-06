import torch
from model.model import MiniLLM
from model.tokenizer import CharTokenizer

tokenizer = CharTokenizer()
model = MiniLLM(tokenizer.vocab_size)
model.load_state_dict(torch.load("model/mini_llm.pth"))
model.eval()

def gerar_json_do_modelo(comando, max_len=128):
    x = tokenizer.encode(comando)
    x = x + [0]*(max_len-len(x)) if len(x)<max_len else x[:max_len]
    x = torch.tensor([x])
    with torch.no_grad():
        out = model(x)
        tokens = out.argmax(-1)[0]
        json_text = tokenizer.decode(tokens.tolist())
        # Tenta extrair JSON
        import json
        try:
            return json.loads(json_text)
        except:
            return {"erro":"não consegui interpretar"}
