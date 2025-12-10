#!/usr/bin/env python3
"""
Script para iniciar todos os serviços ML
"""
import subprocess
import sys
import os
import signal
import threading
import time
from pathlib import Path

BASE_DIR = Path(__file__).parent.resolve()
PY = sys.executable

def start_fastapi():
    """Inicia o servidor FastAPI"""
    print("🚀 Iniciando FastAPI Server...")
    cmd = [PY, "-m", "uvicorn", "src.main:app", "--host", "0.0.0.0", "--port", "8000", "--reload"]
    
    proc = subprocess.Popen(
        cmd,
        cwd=str(BASE_DIR),
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        bufsize=1,
        universal_newlines=True
    )
    
    # Thread para mostrar logs
    def log_output():
        while True:
            line = proc.stdout.readline()
            if not line and proc.poll() is not None:
                break
            if line:
                print(f"[FASTAPI] {line}", end='')
    
    threading.Thread(target=log_output, daemon=True).start()
    return proc

def start_service(service_name, script_path):
    """Inicia um serviço específico"""
    if not script_path.exists():
        print(f"⚠️  Script não encontrado: {script_path}")
        return None
    
    print(f"🚀 Iniciando {service_name}...")
    
    proc = subprocess.Popen(
        [PY, str(script_path)],
        cwd=str(script_path.parent),
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        bufsize=1,
        universal_newlines=True
    )
    
    def log_output():
        while True:
            line = proc.stdout.readline()
            if not line and proc.poll() is not None:
                break
            if line:
                print(f"[{service_name.upper()}] {line}", end='')
    
    threading.Thread(target=log_output, daemon=True).start()
    return proc

def main():
    """Função principal"""
    print("=" * 50)
    print("🤖 Iniciando Serviços de Machine Learning")
    print("=" * 50)
    
    processes = []
    
    try:
        # Iniciar FastAPI (principal)
        fastapi_proc = start_fastapi()
        if fastapi_proc:
            processes.append(("FASTAPI", fastapi_proc))
        
        # Aguardar FastAPI iniciar
        time.sleep(3)
        
        # Iniciar outros serviços se necessário
        services = {
            # "FACE_AUTH": BASE_DIR / "src" / "face_auth" / "main.py",
            # "YOLO_TESS": BASE_DIR / "src" / "yolo_tesseract" / "main.py",
        }
        
        for name, path in services.items():
            if path.exists():
                proc = start_service(name, path)
                if proc:
                    processes.append((name, proc))
                time.sleep(1)
        
        print("\n" + "=" * 50)
        print("✅ Todos os serviços iniciados!")
        print(f"🌐 API: http://localhost:8000")
        print(f"📚 Docs: http://localhost:8000/docs")
        print(f"📷 Stream: http://localhost:8000/stream/0")
        print("\n🛑 Pressione Ctrl+C para encerrar")
        print("=" * 50 + "\n")
        
        # Manter script rodando
        while True:
            time.sleep(1)
            
    except KeyboardInterrupt:
        print("\n\n🛑 Encerrando serviços...")
    finally:
        # Encerrar processos
        for name, proc in processes:
            if proc and proc.poll() is None:
                print(f"   Parando {name}...")
                proc.terminate()
                try:
                    proc.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    proc.kill()
        
        print("👋 Serviços encerrados.")

if __name__ == "__main__":
    main()