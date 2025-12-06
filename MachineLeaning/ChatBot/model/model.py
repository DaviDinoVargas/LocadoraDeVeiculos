import torch
import torch.nn as nn

class MiniLLM(nn.Module):
    def __init__(self, vocab_size, emb_size=64, n_heads=2, n_layers=2, seq_len=128):
        super().__init__()
        self.seq_len = seq_len
        self.token_emb = nn.Embedding(vocab_size, emb_size)
        self.pos_emb = nn.Embedding(seq_len, emb_size)
        encoder_layer = nn.TransformerEncoderLayer(d_model=emb_size, nhead=n_heads)
        self.transformer = nn.TransformerEncoder(encoder_layer, num_layers=n_layers)
        self.output = nn.Linear(emb_size, vocab_size)

    def forward(self, x):
        positions = torch.arange(self.seq_len, device=x.device).unsqueeze(0)
        x = self.token_emb(x) + self.pos_emb(positions)
        x = self.transformer(x)
        return self.output(x)
