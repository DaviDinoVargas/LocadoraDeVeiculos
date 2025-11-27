import subprocess
import sys
import os
import signal
import threading
import time
from pathlib import Path

# CONFIGURATION: ajuste se necessário
BASE_DIR = Path(__file__).parent.resolve()
PY = sys.executable

# scripts relativos ao BASE_DIR
SCRIPTS = {
    "camera_server": {"path": BASE_DIR / "camera_server.py", "enabled": True},
    "face_auth":     {"path": BASE_DIR / "face_auth" / "main.py", "enabled": True},
    "yolo_tesseract":{"path": BASE_DIR / "yolo_tesseract" / "main.py", "enabled": True},
}

for name in list(SCRIPTS.keys()):
    env_flag = os.environ.get(f"DISABLE_{name.upper()}")
    if env_flag and env_flag not in ("0", "false", "False"):
        SCRIPTS[name]["enabled"] = False

# restart policy: None, 'always' ou 'on-failure'
RESTART_POLICY = os.environ.get("ORCH_RESTART", "none").lower()  # 'always', 'on-failure', 'none'

# Tempo entre tentativas de restart (s)
RESTART_BACKOFF = float(os.environ.get("ORCH_BACKOFF", "2.0"))

procs = {}  # name -> Popen

def pipe_output(name, stream):
    """Thread target: lê linhas de stream e escreve prefixadas."""
    try:
        for line in iter(stream.readline, b''):
            if not line:
                break
            try:
                text = line.decode(errors='replace').rstrip()
            except Exception:
                text = str(line)
            print(f"[{name}] {text}")
    finally:
        try:
            stream.close()
        except Exception:
            pass

def start_process(name, script_path: Path):
    if not script_path.exists():
        print(f"[orch] script for {name} not found: {script_path}")
        return None
    env = os.environ.copy()
    try:
        p = subprocess.Popen(
            [PY, str(script_path)],
            cwd=str(script_path.parent),
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            env=env
        )
    except Exception as e:
        print(f"[orch] erro ao iniciar {name}: {e}")
        return None

    # start thread to pipe output
    t = threading.Thread(target=pipe_output, args=(name, p.stdout), daemon=True)
    t.start()
    print(f"[orch] iniciado {name} pid={p.pid}")
    return p

def stop_all():
    print("[orch] solicitando término de todos os subprocessos...")
    for name, p in list(procs.items()):
        if p and p.poll() is None:
            try:
                print(f"[orch] terminando {name} pid={p.pid} ...")
                p.terminate()
            except Exception as e:
                print(f"[orch] erro terminando {name}: {e}")
    # aguarda grace period
    time.sleep(1.5)
    # força kill se necessário
    for name, p in list(procs.items()):
        if p and p.poll() is None:
            try:
                print(f"[orch] matando {name} pid={p.pid} ...")
                p.kill()
            except Exception as e:
                print(f"[orch] erro matando {name}: {e}")

def orchestrate():
    # inicia enabled scripts
    for name, info in SCRIPTS.items():
        if info.get("enabled"):
            p = start_process(name, info["path"])
            if p:
                procs[name] = p
        else:
            print(f"[orch] {name} está desabilitado (DISABLE_{name.upper()})")

    try:
        # loop de monitoramento básico
        while True:
            # checa cada processo
            for name, p in list(procs.items()):
                if p is None:
                    continue
                ret = p.poll()
                if ret is None:
                    continue  # ainda rodando
                # process terminou
                print(f"[orch] processo {name} terminou com código {ret}")
                # remove do dicionário temporariamente
                procs.pop(name, None)
                # decide se reiniciar
                do_restart = False
                if RESTART_POLICY == "always":
                    do_restart = True
                elif RESTART_POLICY == "on-failure" and ret != 0:
                    do_restart = True
                if do_restart:
                    print(f"[orch] reiniciando {name} em {RESTART_BACKOFF}s (policy={RESTART_POLICY})")
                    time.sleep(RESTART_BACKOFF)
                    newp = start_process(name, SCRIPTS[name]["path"])
                    if newp:
                        procs[name] = newp
                else:
                    print(f"[orch] não vai reiniciar {name} (policy={RESTART_POLICY})")

            # se nenhum processo tiver ativo, encerra
            if not any(p.poll() is None for p in procs.values()):
                print("[orch] nenhum subprocesso ativo, encerrando orquestrador.")
                break
            time.sleep(0.8)

    except KeyboardInterrupt:
        print("[orch] KeyboardInterrupt recebido.")
    except Exception as e:
        print(f"[orch] exceção no orchestrator: {e}")
    finally:
        stop_all()
        print("[orch] encerrado.")

def _handle_signal(sig, frame):
    print(f"[orch] sinal recebido: {sig}. Encerrando...")
    stop_all()
    sys.exit(0)

if __name__ == "__main__":
    # captura sinais POSIX
    try:
        signal.signal(signal.SIGINT, _handle_signal)
        signal.signal(signal.SIGTERM, _handle_signal)
    except Exception:
        pass

    print("[orch] iniciando orquestrador de ML...")
    print(f"[orch] python: {PY}")
    print(f"[orch] scripts: {[k for k,v in SCRIPTS.items() if v['enabled']]}")
    orchestrate()
