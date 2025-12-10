# from fastapi import FastAPI, HTTPException
# from fastapi.middleware.cors import CORSMiddleware
# from fastapi.responses import StreamingResponse
# from pydantic import BaseModel
# import cv2
# import time
# import threading
# from typing import List, Optional
# import uvicorn

# # Importe seus módulos
# from src.yolo_tesseract.service import YoloTessService
# from src.camera_server import list_cameras  # ajuste conforme sua estrutura

# app = FastAPI(
#     title="Locadora ML API",
#     description="API para detecção de placas e reconhecimento facial",
#     version="1.0.0"
# )

# # Configurar CORS
# app.add_middleware(
#     CORSMiddleware,
#     allow_origins=["http://localhost:4200"],  # Angular
#     allow_credentials=True,
#     allow_methods=["*"],
#     allow_headers=["*"],
# )

# # Serviço global
# service = YoloTessService()

# # Dicionário para armazenar capturas de câmera ativas
# active_cameras = {}
# last_detections = []
# detection_lock = threading.Lock()

# # Models
# class StartCameraRequest(BaseModel):
#     camera: int
#     fps: float = 10.0

# class DetectionResult(BaseModel):
#     camera: int
#     plate: str
#     bbox: List[int]
#     timestamp: float
#     confidence: Optional[float] = None

# # Endpoints
# @app.get("/")
# def read_root():
#     return {
#         "message": "Locadora ML API",
#         "version": "1.0.0",
#         "endpoints": {
#             "cameras": "/cameras",
#             "start": "/start",
#             "stop": "/stop/{camera_id}",
#             "detections": "/last",
#             "stream": "/stream/{camera_id}"
#         }
#     }

# @app.get("/cameras")
# def get_cameras():
#     """Lista câmeras disponíveis"""
#     cams = list_cameras(8)
#     return {"cameras": cams}

# @app.post("/start")
# def start_camera(req: StartCameraRequest):
#     """Inicia processamento de uma câmera"""
#     ok = service.start_camera_processing(
#         req.camera, 
#         on_plate_callback=on_plate_detected, 
#         max_fps=req.fps
#     )
#     if not ok:
#         raise HTTPException(status_code=409, detail="Camera already running or cannot start")
#     return {"ok": True, "camera": req.camera, "fps": req.fps}

# @app.post("/stop/{camera_id}")
# def stop_camera(camera_id: int):
#     """Para o processamento de uma câmera"""
#     # Libera stream se estiver ativo
#     if camera_id in active_cameras:
#         active_cameras[camera_id].release()
#         del active_cameras[camera_id]
    
#     # Para o serviço
#     ok = service.stop_camera(camera_id)
#     if not ok:
#         raise HTTPException(status_code=404, detail="Camera not running")
#     return {"ok": True, "camera": camera_id}

# @app.get("/last")
# def get_last_detections(limit: int = 50):
#     """Obtém últimas detecções"""
#     with detection_lock:
#         return {"detections": last_detections[:limit]}

# @app.get("/stream/{camera_id}")
# def video_stream(camera_id: int):
#     """Stream MJPEG da câmera"""
#     def generate():
#         cap = cv2.VideoCapture(camera_id)
#         if not cap.isOpened():
#             yield b"data: Camera not available\n\n"
#             return
        
#         active_cameras[camera_id] = cap
        
#         try:
#             while True:
#                 ret, frame = cap.read()
#                 if not ret:
#                     break
                
#                 # Redimensionar para melhor performance
#                 frame = cv2.resize(frame, (640, 480))
                
#                 # Codificar como JPEG
#                 _, buffer = cv2.imencode('.jpg', frame, 
#                     [cv2.IMWRITE_JPEG_QUALITY, 70])
                
#                 yield (b'--frame\r\n'
#                        b'Content-Type: image/jpeg\r\n\r\n' + 
#                        buffer.tobytes() + b'\r\n')
                
#                 time.sleep(0.033)  # ~30 FPS
#         finally:
#             if camera_id in active_cameras:
#                 active_cameras[camera_id].release()
#                 del active_cameras[camera_id]

#     return StreamingResponse(
#         generate(), 
#         media_type="multipart/x-mixed-replace; boundary=frame"
#     )

# @app.get("/health")
# def health_check():
#     """Endpoint de saúde"""
#     return {
#         "status": "healthy",
#         "timestamp": time.time(),
#         "active_cameras": len(active_cameras),
#         "last_detections": len(last_detections)
#     }

# # Callback para detecções
# def on_plate_detected(payload):
#     """Callback quando uma placa é detectada"""
#     with detection_lock:
#         detection = DetectionResult(
#             camera=payload['camera'],
#             plate=payload['plate'],
#             bbox=payload['bbox'],
#             timestamp=time.time(),
#             confidence=payload.get('confidence')
#         )
#         last_detections.append(detection.dict())
        
#         # Mantém apenas as últimas 200 detecções
#         if len(last_detections) > 200:
#             last_detections.pop(0)

# # Handler para shutdown
# @app.on_event("shutdown")
# def shutdown_event():
#     """Libera recursos ao desligar"""
#     print("Shutting down...")
#     for cam_id, cap in active_cameras.items():
#         try:
#             cap.release()
#         except:
#             pass
#     active_cameras.clear()

# # Se rodar diretamente
# if __name__ == "__main__":
#     uvicorn.run(
#         "src.main:app",
#         host="0.0.0.0",
#         port=8000,
#         reload=True,
#         log_level="info"
#     )