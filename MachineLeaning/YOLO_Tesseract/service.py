import threading
import time
import cv2
import os
from utils import log, ensure_dir
from config import YOLO_MODEL, MIN_CONFIDENCE, SAVE_CROPS, CROPS_DIR, MIN_PLATE_AREA

class YoloTessService:
    def __init__(self, model_path=YOLO_MODEL):
        self.detector = YoloDetector(model_path)
        self.running = {}
        self.lock = threading.Lock()   # protege acesso a self.running
        ensure_dir(CROPS_DIR)

    def start_camera_processing(self, cam_index, on_plate_callback=None, max_fps=10):
        with self.lock:
            if cam_index in self.running:
                log.info('camera %s already running', cam_index)
                return False
            t = threading.Thread(target=self._process_loop, args=(cam_index, on_plate_callback, max_fps), daemon=True)
            self.running[cam_index] = {'thread': t, 'stop': False}
            t.start()
            return True

    def stop_camera(self, cam_index):
        with self.lock:
            entry = self.running.get(cam_index)
            if not entry:
                return False
            entry['stop'] = True
        return True

    def _process_loop(self, cam_index, on_plate_callback, max_fps):
        cap = open_camera(cam_index)
        if not cap or not cap.isOpened():
            log.error('cannot open camera %s', cam_index)
            with self.lock:
                self.running.pop(cam_index, None)
            return

        interval = 1.0 / max_fps
        last = 0.0
        try:
            while True:
                # verificação segura do flag 'stop'
                with self.lock:
                    entry = self.running.get(cam_index)
                    if not entry or entry.get('stop'):
                        break

                ret, frame = cap.read()
                if not ret:
                    time.sleep(0.01)
                    continue

                now = time.time()
                if now - last < interval:
                    # evitar o travamento da cpu
                    time.sleep(0.001)
                    continue

                last = now
                # infer
                dets = self.detector.predict(frame, conf=MIN_CONFIDENCE)
                # filtrar candidatos na identificação da placa
                candidates = find_plate_candidates(dets)
                for cand in candidates:
                    x1, y1, x2, y2 = cand['box']
                    area = (x2 - x1) * (y2 - y1)
                    if area < MIN_PLATE_AREA:
                        continue
                    crop = crop_box(frame, (x1, y1, x2, y2), margin=0.1)
                    if crop is None or getattr(crop, "size", 0) == 0:
                        continue
                    if SAVE_CROPS:
                        save_crop(frame=crop, out_dir=CROPS_DIR, prefix=f'cam{cam_index}')
                    # faz OCR
                    text = ocr_plate_image(crop)
                    if text:
                        log.info('Detected plate: %s (cam %s)', text, cam_index)
                        if on_plate_callback:
                            try:
                                on_plate_callback({
                                    'camera': cam_index,
                                    'plate': text,
                                    'bbox': cand['box'],
                                })
                            except Exception as e:
                                log.exception('Erro no callback da câmera %s: %s', cam_index, e)
                # evitar fazer loop quando não há detecção
                time.sleep(0.001)
        except Exception as e:
            log.exception('Erro no loop de processamento da câmera %s: %s', cam_index, e)
        finally:
            try:
                cap.release()
            except Exception:
                pass
            with self.lock:
                self.running.pop(cam_index, None)

    def list_running(self):
        with self.lock:
            return list(self.running.keys())
