import json
import torch
from torch.utils.data import DataLoader, random_split
from model.model import MiniLLM
from model.tokenizer import CharTokenizer
from train import ComandoDataset, data

# --- Config ---
device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
tokenizer = CharTokenizer()

# --- Dataset & split ---
dataset = ComandoDataset(data, tokenizer)
val_frac = 0.1
n = len(dataset)
n_val = max(1, int(n * val_frac))
n_train = n - n_val
_, val_ds = random_split(dataset, [n_train, n_val])
val_loader = DataLoader(val_ds, batch_size=8, shuffle=False)

# --- Modelo ---
model = MiniLLM(tokenizer.vocab_size)
model.load_state_dict(torch.load("model/mini_llm.pth", map_location=device))
model.to(device)
model.eval()

# --- Critério ---
pad_token = 0
criterion_eval = torch.nn.CrossEntropyLoss(ignore_index=pad_token, reduction="sum")

# --- Avaliação ---
def evaluate(model, dataloader, tokenizer):
    total_loss_sum = 0.0
    total_nonpad_tokens = 0
    total_correct_tokens = 0
    total_examples = 0
    json_parse_success = 0
    exact_match = 0

    with torch.no_grad():
        for xb, yb in dataloader:
            xb, yb = xb.to(device), yb.to(device)
            logits = model(xb)  # [B, S, V]
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

    return {
        "avg_loss_per_token": avg_loss_per_token,
        "perplexity": perplexity,
        "token_accuracy": token_accuracy,
        "json_parse_rate": json_parse_rate,
        "exact_match_rate": exact_match_rate,
        "total_examples": total_examples,
        "total_nonpad_tokens": total_nonpad_tokens
    }

if __name__ == "__main__":
    metrics = evaluate(model, val_loader, tokenizer)
    print("VAL METRICS:", metrics)
