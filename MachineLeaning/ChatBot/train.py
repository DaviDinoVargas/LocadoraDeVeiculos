import json
import torch
from torch.utils.data import DataLoader, Dataset, random_split
import torch.nn as nn
from model.model import MiniLLM
from model.tokenizer import CharTokenizer

# --- Dataset ---
with open("dataset/comandos.json") as f:
    data = json.load(f)

tokenizer = CharTokenizer()

class ComandoDataset(Dataset):
    def __init__(self, data, tokenizer, seq_len=128):
        self.data = data
        self.tokenizer = tokenizer
        self.seq_len = seq_len

    def __len__(self):
        return len(self.data)

    def __getitem__(self, idx):
        comando = self.data[idx]["comando"]
        resposta = json.dumps(self.data[idx]["json"])
        x = self.tokenizer.encode(comando)
        y = self.tokenizer.encode(resposta)
        # Pad
        x = x + [0]*(self.seq_len - len(x)) if len(x)<self.seq_len else x[:self.seq_len]
        y = y + [0]*(self.seq_len - len(y)) if len(y)<self.seq_len else y[:self.seq_len]
        return torch.tensor(x), torch.tensor(y)

# --- Treino ---
def train_model(epochs=50, batch_size=2, lr=1e-3):
    dataset = ComandoDataset(data, tokenizer)
    dataloader = DataLoader(dataset, batch_size=batch_size, shuffle=True)

    model = MiniLLM(tokenizer.vocab_size)
    criterion = nn.CrossEntropyLoss()
    optimizer = torch.optim.Adam(model.parameters(), lr=lr)

    for epoch in range(epochs):
        total_loss = 0
        for x, y in dataloader:
            optimizer.zero_grad()
            out = model(x)
            out = out.view(-1, tokenizer.vocab_size)
            y = y.view(-1)
            loss = criterion(out, y)
            loss.backward()
            optimizer.step()
            total_loss += loss.item()
        print(f"Epoch {epoch+1}, Loss: {total_loss/len(dataloader):.4f}")

    torch.save(model.state_dict(), "model/mini_llm.pth")
    print("Treinamento finalizado e modelo salvo em 'model/mini_llm.pth'.")
    return model, dataset

# --- Avaliação ---
def evaluate_model(model, dataset, val_frac=0.1, batch_size=8):
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model.to(device)
    model.eval()

    # Split val
    n = len(dataset)
    n_val = max(1, int(n * val_frac))
    n_train = n - n_val
    _, val_ds = random_split(dataset, [n_train, n_val])
    val_loader = DataLoader(val_ds, batch_size=batch_size, shuffle=False)

    pad_token = 0
    criterion_eval = nn.CrossEntropyLoss(ignore_index=pad_token, reduction="sum")

    total_loss_sum = 0.0
    total_nonpad_tokens = 0
    total_correct_tokens = 0
    total_examples = 0
    json_parse_success = 0
    exact_match = 0

    with torch.no_grad():
        for xb, yb in val_loader:
            xb, yb = xb.to(device), yb.to(device)
            logits = model(xb)
            B, S, V = logits.shape
            logits_flat = logits.view(-1, V)
            targets_flat = yb.view(-1)

            # loss
            loss_sum = criterion_eval(logits_flat, targets_flat)
            total_loss_sum += loss_sum.item()

            # token accuracy
            nonpad_mask = (targets_flat != pad_token)
            n_nonpad = int(nonpad_mask.sum().item())
            total_nonpad_tokens += n_nonpad
            preds_flat = logits_flat.argmax(dim=-1)
            correct = ((preds_flat == targets_flat) & nonpad_mask).sum().item()
            total_correct_tokens += correct

            # per-example JSON parse & exact match
            preds = preds_flat.view(B, S).cpu().tolist()
            targets = yb.cpu().tolist()
            for p_tokens, t_tokens in zip(preds, targets):
                pred_text = tokenizer.decode(p_tokens).split('\x00', 1)[0].strip()
                target_text = tokenizer.decode(t_tokens).split('\x00', 1)[0].strip()
                try:
                    json.loads(pred_text)
                    json_parse_success += 1
                except:
                    pass
                if pred_text == target_text:
                    exact_match += 1
                total_examples += 1

    avg_loss_per_token = total_loss_sum / max(1, total_nonpad_tokens)
    perplexity = float(torch.exp(torch.tensor(avg_loss_per_token)))
    token_accuracy = total_correct_tokens / max(1, total_nonpad_tokens)
    json_parse_rate = json_parse_success / max(1, total_examples)
    exact_match_rate = exact_match / max(1, total_examples)

    metrics = {
        "avg_loss_per_token": avg_loss_per_token,
        "perplexity": perplexity,
        "token_accuracy": token_accuracy,
        "json_parse_rate": json_parse_rate,
        "exact_match_rate": exact_match_rate,
        "total_examples": total_examples,
        "total_nonpad_tokens": total_nonpad_tokens
    }
    return metrics

# --- Main ---
if __name__ == "__main__":
    model, dataset = train_model(epochs=50)
    metrics = evaluate_model(model, dataset)
    print("\n--- VAL METRICS ---")
    for k, v in metrics.items():
        print(f"{k}: {v}")
