# main.py (ÚNICA VERSÃO)
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import StreamingResponse
from pydantic import BaseModel
import cv2
import time
import threading
import sys
import os
from typing import List, Optional
import numpy as np

# Configurar o caminho para importar módulos corretamente
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

app = FastAPI(
    title="Locadora ML API",
    description="API para detecção de placas e reconhecimento facial",
    version="1.0.0"
)

# Configurar CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:4200"],  # Angular
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Importar módulos locais
try:
    # Importar camera_server
    try:
        from camera_server import list_cameras, open_camera
        print("✅ Módulo camera_server importado com sucesso")
    except ImportError:
        # Fallback se camera_server não existir
        print("⚠️  camera_server.py não encontrado, usando mock")
        def list_cameras(max_probe=8):
            return [0, 1]  # Câmeras mock
        
        def open_camera(idx, width=None, height=None):
            return None
    
    # Importar yolo_tesseract
    try:
        from yolo_tesseract.service import YoloTessService
        print("✅ Módulo yolo_tesseract importado com sucesso")
        service = YoloTessService()
        HAS_YOLO = True
    except ImportError as e:
        print(f"⚠️  Erro ao importar yolo_tesseract: {e}")
        
        # Mock para desenvolvimento
        class MockYoloTessService:
            def start_camera_processing(self, camera, on_plate_callback=None, max_fps=10):
                print(f"[MOCK] Iniciando câmera {camera}")
                return True
            
            def stop_camera(self, camera):
                print(f"[MOCK] Parando câmera {camera}")
                return True
        
        service = MockYoloTessService()
        HAS_YOLO = False
        
except Exception as e:
    print(f"❌ Erro crítico ao importar módulos: {e}")
    print("💡 Verifique se os arquivos existem e têm as classes/funções corretas")
    HAS_YOLO = False

# Dicionário para armazenar capturas de câmera ativas
active_cameras = {}
last_detections = []
detection_lock = threading.Lock()

# Models
class StartCameraRequest(BaseModel):
    camera: int
    fps: float = 10.0

class DetectionResult(BaseModel):
    camera: int
    plate: str
    bbox: List[int]
    timestamp: float
    confidence: Optional[float] = None

# Endpoints
@app.get("/")
def read_root():
    return {
        "message": "Locadora ML API",
        "version": "1.0.0",
        "yolo_available": HAS_YOLO,
        "endpoints": {
            "cameras": "/cameras",
            "start": "/start",
            "stop": "/stop/{camera_id}",
            "detections": "/last",
            "stream": "/stream/{camera_id}",
            "health": "/health",
            "test": "/test/{camera_id}"
        }
    }

@app.get("/cameras")
def get_cameras():
    """Lista câmeras disponíveis"""
    try:
        cams = list_cameras(8)
        return {"cameras": cams, "count": len(cams)}
    except Exception as e:
        return {"cameras": [], "error": str(e), "count": 0}

@app.post("/start")
def start_camera(req: StartCameraRequest):
    """Inicia processamento de uma câmera"""
    try:
        ok = service.start_camera_processing(
            req.camera, 
            on_plate_callback=on_plate_detected, 
            max_fps=req.fps
        )
        if not ok:
            raise HTTPException(status_code=409, detail="Camera already running or cannot start")
        return {"ok": True, "camera": req.camera, "fps": req.fps}
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error: {str(e)}")

@app.post("/stop/{camera_id}")
def stop_camera(camera_id: int):
    """Para o processamento de uma câmera"""
    # Libera stream se estiver ativo
    if camera_id in active_cameras:
        try:
            active_cameras[camera_id].release()
        except:
            pass
        del active_cameras[camera_id]
    
    # Para o serviço
    try:
        ok = service.stop_camera(camera_id)
        if not ok:
            raise HTTPException(status_code=404, detail="Camera not running")
        return {"ok": True, "camera": camera_id}
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error: {str(e)}")

@app.get("/last")
def get_last_detections(limit: int = 50):
    """Obtém últimas detecções"""
    with detection_lock:
        if not last_detections and not HAS_YOLO:
            # Mock para desenvolvimento
            mock_detections = [
                {
                    "camera": 0,
                    "plate": "ABC1234",
                    "bbox": [100, 100, 300, 200],
                    "timestamp": time.time() - 10,
                    "confidence": 0.85
                },
                {
                    "camera": 1,
                    "plate": "XYZ5678",
                    "bbox": [150, 120, 350, 220],
                    "timestamp": time.time() - 5,
                    "confidence": 0.92
                }
            ]
            return {"detections": mock_detections[:limit], "mock": True}
        
        return {"detections": last_detections[:limit], "mock": False}

@app.get("/stream/{camera_id}")
def video_stream(camera_id: int):
    """Stream MJPEG da câmera"""
    def generate():
        try:
            cap = cv2.VideoCapture(camera_id)
            if not cap.isOpened():
                print(f"❌ Não foi possível abrir câmera {camera_id}")
                # Retorna imagem de fallback
                frame = np.zeros((480, 640, 3), dtype=np.uint8)
                frame[:] = (40, 40, 40)
                cv2.putText(frame, f"Camera {camera_id} OFF", (50, 240),
                           cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 0, 255), 2)
                _, buffer = cv2.imencode('.jpg', frame)
                yield (b'--frame\r\n'
                       b'Content-Type: image/jpeg\r\n\r\n' + 
                       buffer.tobytes() + b'\r\n')
                return
            
            active_cameras[camera_id] = cap
            
            while True:
                ret, frame = cap.read()
                if not ret:
                    break
                
                # Redimensionar para melhor performance
                frame = cv2.resize(frame, (640, 480))
                
                # Adicionar overlay
                cv2.putText(frame, f"Camera {camera_id}", (10, 30),
                           cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                
                # Codificar como JPEG
                _, buffer = cv2.imencode('.jpg', frame, 
                    [cv2.IMWRITE_JPEG_QUALITY, 70])
                
                yield (b'--frame\r\n'
                       b'Content-Type: image/jpeg\r\n\r\n' + 
                       buffer.tobytes() + b'\r\n')
                
                time.sleep(0.033)  # ~30 FPS
                
        except Exception as e:
            print(f"❌ Erro no stream: {e}")
        finally:
            if camera_id in active_cameras:
                try:
                    active_cameras[camera_id].release()
                except:
                    pass
                del active_cameras[camera_id]

    return StreamingResponse(
        generate(), 
        media_type="multipart/x-mixed-replace; boundary=frame"
    )

@app.get("/health")
def health_check():
    """Endpoint de saúde"""
    return {
        "status": "healthy",
        "timestamp": time.time(),
        "yolo_available": HAS_YOLO,
        "active_cameras": len(active_cameras),
        "last_detections": len(last_detections)
    }

@app.get("/test/{camera_id}")
def test_camera(camera_id: int):
    """Testa se uma câmera está funcionando"""
    try:
        cap = cv2.VideoCapture(camera_id)
        if cap.isOpened():
            ret, frame = cap.read()
            cap.release()
            
            if ret:
                return {
                    "status": "working",
                    "camera": camera_id,
                    "message": "Câmera funcionando",
                    "frame_size": f"{frame.shape[1]}x{frame.shape[0]}" if ret else "N/A"
                }
            else:
                return {
                    "status": "error",
                    "camera": camera_id,
                    "message": "Câmera aberta mas não lê frames"
                }
        else:
            return {
                "status": "error",
                "camera": camera_id,
                "message": "Não foi possível abrir"
            }
    except Exception as e:
        return {
            "status": "error",
            "camera": camera_id,
            "message": f"Erro: {str(e)}"
        }

# Callback para detecções
def on_plate_detected(payload):
    """Callback quando uma placa é detectada"""
    with detection_lock:
        detection = DetectionResult(
            camera=payload.get('camera', 0),
            plate=payload.get('plate', 'UNKNOWN'),
            bbox=payload.get('bbox', [0, 0, 100, 100]),
            timestamp=time.time(),
            confidence=payload.get('confidence', 0.0)
        )
        last_detections.append(detection.dict())
        
        # Mantém apenas as últimas 200 detecções
        if len(last_detections) > 200:
            last_detections.pop(0)
        
        print(f"📸 Detecção: {detection.plate} (Câmera {detection.camera})")

# Handler para shutdown
@app.on_event("shutdown")
def shutdown_event():
    """Libera recursos ao desligar"""
    print("🔴 Shutting down...")
    for cam_id, cap in active_cameras.items():
        try:
            cap.release()
        except:
            pass
    active_cameras.clear()

# Se rodar diretamente
if __name__ == "__main__":
    import uvicorn
    
    print("=" * 50)
    print("🤖 Iniciando Locadora ML API")
    print("=" * 50)
    print(f"📦 YOLO disponível: {HAS_YOLO}")
    print(f"🌐 URL: http://localhost:8000")
    print(f"📚 Docs: http://localhost:8000/docs")
    print("=" * 50)
    
    uvicorn.run(
        "main:app",
        host="0.0.0.0",
        port=8000,
        reload=True,
        log_level="info"
    )