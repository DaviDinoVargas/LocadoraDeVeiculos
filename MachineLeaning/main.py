# main.py (ÚNICA VERSÃO)
import sys
import os

# O console do Windows (cmd/PowerShell) roda por padrão num codepage
# (ex.: cp1252) que não sabe representar emoji. Os prints deste arquivo
# usam emoji para deixar o log de inicialização mais legível, e sem isso
# o processo morre com UnicodeEncodeError assim que o primeiro print
# roda — antes até de subir o servidor. reconfigure (Python 3.7+) força
# stdout/stderr para UTF-8 independente do codepage do console.
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

# Antes de qualquer import que traga o cv2: abertura rápida da webcam no Windows (ver yolo_tesseract/__init__.py).
os.environ.setdefault('OPENCV_VIDEOIO_MSMF_ENABLE_HW_TRANSFORMS', '0')

# Configurar o caminho para importar módulos corretamente
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import time
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from shared import settings
from yolo_tesseract import router as cameras
from yolo_tesseract.tesseract_service import ocr_disponivel


@asynccontextmanager
async def lifespan(app: FastAPI):
    yield
    print("🔴 Shutting down...")
    cameras.hub.shutdown()


app = FastAPI(
    title="Locadora ML API",
    description="API para detecção de placas e reconhecimento facial",
    version="1.0.0",
    lifespan=lifespan,
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.CORS_ORIGINS,  # Angular (CORS_ALLOWED_ORIGINS)
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(cameras.router)
app.include_router(cameras.stream_router)
print(f"✅ Módulo de câmeras carregado (YOLO disponível: {cameras.HAS_YOLO})")

# Reconhecimento facial (MediaPipe FaceMesh) e chatbot de regras/busca
try:
    from FaceAuth.router import router as face_router
    app.include_router(face_router)
    print("✅ Módulo FaceAuth importado com sucesso")
except ImportError as e:
    print(f"⚠️  Erro ao importar FaceAuth: {e}")

try:
    from ChatBot.router import router as chat_router
    app.include_router(chat_router)
    print("✅ Módulo ChatBot importado com sucesso")
except ImportError as e:
    print(f"⚠️  Erro ao importar ChatBot: {e}")


@app.get("/")
def read_root():
    return {"message": "Locadora ML API", "version": "1.0.0", "docs": "/docs"}


@app.get("/health")
def health_check():
    """Endpoint de saúde (público: só estado agregado, sem dados de ninguém)"""
    ocr_ok, ocr_detalhe = ocr_disponivel()
    return {
        "status": "healthy",
        "timestamp": time.time(),
        "yolo_available": cameras.HAS_YOLO,
        "ocr_available": ocr_ok,
        "ocr_detail": ocr_detalhe,
        "active_cameras": len(cameras.hub.abertas()),
        "last_detections": cameras.total_deteccoes(),
    }


# Se rodar diretamente
if __name__ == "__main__":
    import uvicorn

    print("=" * 50)
    print("🤖 Iniciando Locadora ML API")
    print("=" * 50)
    print(f"📦 YOLO disponível: {cameras.HAS_YOLO}")
    print(f"🌐 URL: http://localhost:8000")
    print(f"📚 Docs: http://localhost:8000/docs")
    print("=" * 50)

    uvicorn.run(
        "main:app",
        host="0.0.0.0",
        port=8000,
        reload=True,
        log_level="info",
        # O stream MJPEG mantém a conexão aberta indefinidamente; sem um limite o uvicorn
        # esperava ela fechar para recarregar/desligar e o serviço ficava fora do ar.
        timeout_graceful_shutdown=3,
    )
