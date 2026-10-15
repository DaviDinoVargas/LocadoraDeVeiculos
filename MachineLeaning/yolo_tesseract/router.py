# Endpoints de câmera e leitura de placas. Tudo aqui (exceto o stream, que usa
# ticket) exige o JWT de um usuário logado: antes qualquer um que alcançasse o
# proxy podia ligar/desligar a webcam do servidor e ler as placas detectadas.
import asyncio
import os
import threading
import time
from typing import List, Optional

import cv2
from fastapi import APIRouter, Depends, HTTPException, Path, Request
from fastapi.responses import StreamingResponse
from pydantic import BaseModel, Field

from shared import auth, limites
from shared.camera_manager import CameraHub

from .camera_server import list_cameras, open_camera
from .utils import log

hub = CameraHub(open_camera)

# Serviço YOLO + OCR. Se o modelo não carregar, os demais endpoints continuam
# de pé e /start explica o motivo (em vez do modo "mock" antigo, que devolvia
# placas inventadas).
service = None
erro_yolo: str | None = None
try:
    from .service import YoloTessService
    service = YoloTessService(hub)
except Exception as e:  # noqa: BLE001 - ultralytics/torch/modelo ausente
    erro_yolo = str(e)
    log.error('YOLO indisponível: %s', e)

HAS_YOLO = service is not None

# S10: a autenticação vem primeiro (preenche request.state.usuario) para o limite contar por usuário, não por IP.
router = APIRouter(tags=["cameras"], dependencies=[Depends(auth.exigir_dono_das_cameras),
                                                   Depends(limites.limitar("cameras", maximo=300, janela_s=60))])
stream_router = APIRouter(tags=["cameras"])

# Ações que abrem/fecham hardware ou emitem credencial: bem mais restritas que a leitura (/last, /cameras).
_limite_acao = Depends(limites.limitar("cameras-acao", maximo=30, janela_s=60))

_detections: list[dict] = []
_lock = threading.Lock()
MAX_DETECCOES = 200
FPS_MAXIMO_STREAM = 20


# Índice de câmera plausível (a busca só olha os primeiros): número negativo ou gigante (acima de 64 bits) quebrava o OpenCV com 500.
MAXIMO_INDICE_CAMERA = 63
IndiceDeCamera = Path(ge=0, le=MAXIMO_INDICE_CAMERA)


class StartCameraRequest(BaseModel):
    camera: int = Field(ge=0, le=MAXIMO_INDICE_CAMERA)
    # 0, negativo ou milhares de quadros por segundo travariam a CPU da máquina que roda o ML: limite sensato, 422 fora dele
    fps: float = Field(default=10.0, gt=0, le=30, allow_inf_nan=False)


class DetectionResult(BaseModel):
    camera: int
    plate: str
    bbox: List[int]
    timestamp: float
    confidence: Optional[float] = None


def registrar_deteccao(payload: dict) -> None:
    """Callback do serviço quando uma placa válida é lida."""
    deteccao = DetectionResult(
        camera=payload.get('camera', 0),
        plate=payload['plate'],
        bbox=payload.get('bbox', [0, 0, 0, 0]),
        timestamp=time.time(),
        confidence=payload.get('confidence'),
    ).model_dump()
    with _lock:
        _detections.append(deteccao)
        if len(_detections) > MAX_DETECCOES:
            _detections.pop(0)


def total_deteccoes() -> int:
    with _lock:
        return len(_detections)


# Procurar câmeras abre cada índice em sequência e, no Windows, índice que não existe também demora.
# Por isso: poucos índices, resultado em cache e uma busca por vez (`?atualizar=true` força).
MAX_PROBE = int(os.environ.get('CAMERA_MAX_PROBE', 4))
VALIDADE_LISTA_S = 60.0
_lista_cache: tuple[float, list[int]] | None = None
_lista_lock = threading.Lock()


def _procurar_cameras(atualizar: bool, abertas: list[int]) -> list[int]:
    global _lista_cache
    with _lista_lock:
        agora = time.monotonic()
        if not atualizar and _lista_cache and agora - _lista_cache[0] < VALIDADE_LISTA_S:
            achadas = _lista_cache[1]
        else:
            achadas = list_cameras(MAX_PROBE, ignorar=abertas)
            _lista_cache = (agora, achadas)
    return sorted(set(achadas) | set(abertas))


@router.get("/cameras")
def get_cameras(atualizar: bool = False):
    """Lista câmeras disponíveis"""
    abertas = hub.abertas()
    try:
        cams = _procurar_cameras(atualizar, abertas)
        return {"cameras": cams, "count": len(cams)}
    except Exception as e:  # noqa: BLE001
        log.exception('Erro ao listar câmeras: %s', e)
        return {"cameras": abertas, "error": "Falha ao procurar câmeras", "count": len(abertas)}


@router.post("/start", dependencies=[_limite_acao])
def start_camera(req: StartCameraRequest):
    """Inicia processamento de uma câmera"""
    if service is None:
        raise HTTPException(status_code=503, detail=f"Detecção de placas indisponível: {erro_yolo}")
    ok, motivo = service.start_camera_processing(req.camera, on_plate_callback=registrar_deteccao, max_fps=req.fps)
    if not ok:
        # OCR ausente é problema do servidor (503); o resto é conflito/estado (409).
        status = 503 if 'Tesseract' in motivo else 409
        raise HTTPException(status_code=status, detail=motivo)
    return {"ok": True, "camera": req.camera, "fps": req.fps}


@router.post("/stop/{camera_id}", dependencies=[_limite_acao])
def stop_camera(camera_id: int = IndiceDeCamera):
    """Para o processamento de uma câmera"""
    if service is None or not service.stop_camera(camera_id):
        raise HTTPException(status_code=404, detail="Camera not running")
    return {"ok": True, "camera": camera_id}


@router.get("/last")
def get_last_detections(limit: int = 50, since: float = 0.0, camera: Optional[int] = None):
    """Últimas detecções, da mais nova para a mais antiga. `since` (epoch em
    segundos) devolve só as posteriores -- usado pelo polling do Angular."""
    if service is not None:
        service.registrar_contato()   # o navegador está acompanhando: não solta a câmera por ociosidade
    with _lock:
        itens = [d for d in _detections if d['timestamp'] > since and (camera is None or d['camera'] == camera)]
    itens.reverse()
    return {"detections": itens[:max(limit, 0)], "yolo_available": HAS_YOLO}


@router.get("/test/{camera_id}", dependencies=[_limite_acao])
def test_camera(camera_id: int = IndiceDeCamera):
    """Testa se uma câmera está funcionando (usa o capturador compartilhado)."""
    if not hub.acquire(camera_id):
        return {"status": "error", "camera": camera_id, "message": "Não foi possível abrir"}
    try:
        frame, _ = hub.read(camera_id, 0, timeout=3.0)
    finally:
        hub.release(camera_id)
    if frame is None:
        return {"status": "error", "camera": camera_id, "message": "Câmera aberta mas não lê frames"}
    return {
        "status": "working",
        "camera": camera_id,
        "message": "Câmera funcionando",
        "frame_size": f"{frame.shape[1]}x{frame.shape[0]}",
    }


@router.post("/stream-ticket", dependencies=[_limite_acao])
def emitir_ticket(usuario: auth.Usuario = Depends(auth.exigir_dono_das_cameras)):
    """O <img> do stream não envia Authorization; o Angular troca o JWT por um
    ticket de uso único (30 s) e o passa na URL do stream."""
    return auth.emitir_ticket_stream(usuario)


def _desenhar(frame, camera_id, itens):
    for item in itens:
        x1, y1, x2, y2 = item['box']
        cor = (0, 200, 0) if item.get('placa') else (0, 165, 255)
        cv2.rectangle(frame, (x1, y1), (x2, y2), cor, 2)
        cv2.putText(frame, item['label'], (x1, max(y1 - 6, 14)), cv2.FONT_HERSHEY_SIMPLEX, 0.6, cor, 2)
    cv2.putText(frame, f"Camera {camera_id}", (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)


@stream_router.get("/stream/{camera_id}", dependencies=[Depends(limites.limitar("stream", maximo=20, janela_s=60)), Depends(auth.consumir_ticket_stream)])
async def video_stream(request: Request, camera_id: int = IndiceDeCamera):
    """Stream MJPEG da câmera, com as caixas do YOLO desenhadas por cima."""
    if not await asyncio.to_thread(hub.acquire, camera_id):
        raise HTTPException(status_code=503, detail=f"Não foi possível abrir a câmera {camera_id}")

    async def gerar():
        seq = 0
        ultimo_envio = 0.0
        try:
            while not await request.is_disconnected():
                if service is not None:
                    service.registrar_contato()
                frame, seq = await asyncio.to_thread(hub.read, camera_id, seq, 1.0)
                if frame is None:
                    if not hub.esta_aberta(camera_id):
                        break
                    continue
                if time.monotonic() - ultimo_envio < 1.0 / FPS_MAXIMO_STREAM:
                    continue
                ultimo_envio = time.monotonic()

                # As caixas do detector estão em coordenadas do frame original:
                # desenha antes de reduzir para o stream.
                _desenhar(frame, camera_id, hub.overlay(camera_id))
                frame = cv2.resize(frame, (640, 480))
                ok, buffer = cv2.imencode('.jpg', frame, [cv2.IMWRITE_JPEG_QUALITY, 70])
                if not ok:
                    continue
                yield (b'--frame\r\nContent-Type: image/jpeg\r\n\r\n' + buffer.tobytes() + b'\r\n')
        finally:
            hub.release(camera_id)

    return StreamingResponse(gerar(), media_type="multipart/x-mixed-replace; boundary=frame")
