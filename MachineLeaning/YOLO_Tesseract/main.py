# main.py
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from camera_client import list_cameras
from service import YoloTessService
from starlette.responses import JSONResponse
import uvicorn

app = FastAPI(title='YOLO_Tesseract Service')
service = YoloTessService()
class StartReq(BaseModel):
    camera: int
    fps: float = 10.0

@app.get('/cameras')
def cameras():
    cams = list_cameras(8)
    return {'cameras': cams}

@app.post('/start')
def start(req: StartReq):
    ok = service.start_camera_processing(
        req.camera, 
        on_plate_callback=on_plate_detected, 
        max_fps=req.fps
    )
    if not ok:
        raise HTTPException(status_code=409, detail='camera already running or cannot start')
    return {'ok': True}

@app.post('/stop/{cam}')
def stop(cam: int):
    ok = service.stop_camera(cam)
    if not ok:
        raise HTTPException(status_code=404, detail='camera not running')
    return {'ok': True}

# store last detections in memory (simple pub-sub)
LAST = []

def on_plate_detected(payload):
    LAST.append(payload)
    # keep only recent
    if len(LAST) > 200:
        LAST.pop(0)

@app.get('/last')
def last():
    return JSONResponse(LAST)

if __name__ == '__main__':
    uvicorn.run('main:app', host='0.0.0.0', port=8000, reload=False)
