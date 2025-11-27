# yolo_detector.py
from ultralytics import YOLO
import numpy as np


class YoloDetector:
    def __init__(self, model_path):
        self.model = YOLO(model_path)


    def predict(self, frame, imgsz=640, conf=0.25):
# frame: BGR numpy array
# returns list of detections: [{ 'box':(x1,y1,x2,y2), 'conf':..., 'class':..., 'label':... }, ...]
        res = self.model.predict(frame, imgsz=imgsz, conf=conf, verbose=False)
        out = []
# ultralytics returns a list of Results (one per image). Aqui single-image
        if len(res) == 0:
            return out
        r = res[0]
        boxes = r.boxes
        if boxes is None:
            return out
        for b in boxes:
            xyxy = b.xyxy[0].cpu().numpy() # x1,y1,x2,y2
            conf = float(b.conf[0].cpu().numpy())
            cls = int(b.cls[0].cpu().numpy())
            label = self.model.model.names.get(cls, str(cls)) if hasattr(self.model, 'model') else str(cls)
            out.append({'box': tuple(map(int, xyxy)), 'conf': conf, 'class': cls, 'label': label})
        return out