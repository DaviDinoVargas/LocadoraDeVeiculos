import threading
import time

import pytesseract

from .config import YOLO_MODEL, MIN_CONFIDENCE, SAVE_CROPS, CROPS_DIR, MIN_PLATE_AREA
from .plate_localizer import PLATE_CLASSES, VEHICLE_CLASSES, find_plate_candidates, find_plate_regions, crop_box, save_crop
from .tesseract_service import ocr_plate_image, ocr_disponivel
from .utils import log, ensure_dir
from .yolo_detector import YoloDetector

# Mesma placa na mesma câmera dentro desta janela não dispara o callback de
# novo (a 10 fps o mesmo carro parado geraria dezenas de "detecções" por segundo).
JANELA_REPETICAO_S = 5.0
# Uma leitura só vale depois de repetida: o OCR erra um caractere de vez em quando, mas
# raramente erra igual em frames diferentes. Exige a mesma placa N vezes na janela abaixo.
LEITURAS_PARA_CONFIRMAR = 2
JANELA_CONFIRMACAO_S = 3.0
# Quantos erros por frame logar antes de silenciar (evita inundar o log).
MAX_ERROS_LOGADOS = 5


class YoloTessService:
    def __init__(self, hub, model_path=YOLO_MODEL):
        self.hub = hub
        self.detector = YoloDetector(model_path)
        self.running = {}
        self.lock = threading.Lock()   # protege acesso a self.running
        ensure_dir(CROPS_DIR)

    def start_camera_processing(self, cam_index, on_plate_callback=None, max_fps=10):
        """Devolve (ok, motivo). `motivo` explica a recusa para a API responder
        algo útil em vez de um 409 genérico."""
        ok_ocr, detalhe = ocr_disponivel()
        if not ok_ocr:
            return False, detalhe

        with self.lock:
            if cam_index in self.running:
                log.info('camera %s already running', cam_index)
                return False, 'A câmera já está em processamento.'

            if not self.hub.acquire(cam_index):
                return False, f'Não foi possível abrir a câmera {cam_index}.'

            t = threading.Thread(
                target=self._process_loop, args=(cam_index, on_plate_callback, max_fps),
                daemon=True, name=f'yolo-{cam_index}',
            )
            self.running[cam_index] = {'thread': t, 'stop': False}
            t.start()
            return True, ''

    def stop_camera(self, cam_index):
        with self.lock:
            entry = self.running.get(cam_index)
            if not entry:
                return False
            entry['stop'] = True
        return True

    def _deve_parar(self, cam_index):
        with self.lock:
            entry = self.running.get(cam_index)
            return not entry or entry.get('stop')

    def _process_loop(self, cam_index, on_plate_callback, max_fps):
        interval = 1.0 / max(max_fps, 0.1)
        last = 0.0
        seq = 0
        vistas = {}      # placa -> instante em que o callback foi chamado
        leituras = {}    # placa -> instantes das leituras ainda não confirmadas
        erros = 0
        try:
            while not self._deve_parar(cam_index):
                frame, seq = self.hub.read(cam_index, seq, timeout=0.5)
                if frame is None:
                    if not self.hub.esta_aberta(cam_index):
                        log.error('camera %s deixou de responder; encerrando o processamento', cam_index)
                        break
                    continue

                now = time.time()
                if now - last < interval:
                    continue
                last = now

                # Um frame problemático (OCR, recorte, callback) não pode derrubar a
                # thread: antes qualquer exceção aqui encerrava o monitoramento em silêncio.
                try:
                    self._processar_frame(cam_index, frame, on_plate_callback, vistas, now, leituras)
                except pytesseract.TesseractNotFoundError:
                    log.error('Tesseract desapareceu durante o processamento da câmera %s', cam_index)
                    break
                except Exception as e:  # noqa: BLE001
                    erros += 1
                    if erros <= MAX_ERROS_LOGADOS:
                        log.exception('Erro ao processar frame da câmera %s: %s', cam_index, e)
        except Exception as e:  # noqa: BLE001
            log.exception('Erro no loop de processamento da câmera %s: %s', cam_index, e)
        finally:
            self.hub.publicar_overlay(cam_index, [])
            self.hub.release(cam_index)
            with self.lock:
                self.running.pop(cam_index, None)

    def _regioes_de_placa(self, frame, dets):
        """Onde procurar a placa: dentro de cada veículo detectado (localizador de retângulo com
        cara de placa; o recorte "terço inferior" só entra se nada for encontrado) e, se o YOLO
        não viu nenhum veículo (ex.: foto só da placa), na imagem inteira."""
        regioes = []
        veiculos = 0
        for d in dets:
            label = d.get('label', '').lower()
            if any(p in label for p in PLATE_CLASSES):
                regioes.append((d['box'], d.get('conf')))
            elif any(v in label for v in VEHICLE_CLASSES):
                veiculos += 1
                achadas = find_plate_regions(frame, d['box'])
                if achadas:
                    regioes.extend((r, d.get('conf')) for r in achadas)
                else:
                    heuristicos = find_plate_candidates([d])
                    regioes.extend((h['box'], d.get('conf')) for h in heuristicos)
        if veiculos == 0 and not regioes:
            regioes.extend((r, None) for r in find_plate_regions(frame, max_regions=2))
        return regioes

    def _confirmou(self, leituras, placa, now):
        recentes = [t for t in leituras.get(placa, []) if now - t <= JANELA_CONFIRMACAO_S]
        recentes.append(now)
        leituras[placa] = recentes
        for chave in [k for k, v in leituras.items() if not v or now - v[-1] > JANELA_CONFIRMACAO_S]:
            del leituras[chave]
        return len(recentes) >= LEITURAS_PARA_CONFIRMAR

    def _processar_frame(self, cam_index, frame, on_plate_callback, vistas, now, leituras):
        dets = self.detector.predict(frame, conf=MIN_CONFIDENCE)
        overlay = [{'box': d['box'], 'label': f"{d['label']} {d['conf']:.0%}"} for d in dets]

        for box, conf in self._regioes_de_placa(frame, dets):
            x1, y1, x2, y2 = box
            if (x2 - x1) * (y2 - y1) < MIN_PLATE_AREA:
                continue
            crop = crop_box(frame, (x1, y1, x2, y2), margin=0.0)
            if crop is None or getattr(crop, 'size', 0) == 0:
                continue
            if SAVE_CROPS:
                save_crop(crop, CROPS_DIR, prefix=f'cam{cam_index}')

            placa = ocr_plate_image(crop)
            if not placa:
                overlay.append({'box': box, 'label': '?', 'placa': False})
                continue
            overlay.append({'box': box, 'label': placa, 'placa': True})

            if now - vistas.get(placa, 0.0) < JANELA_REPETICAO_S:
                continue
            if not self._confirmou(leituras, placa, now):
                continue
            vistas[placa] = now
            log.info('Detected plate: %s (cam %s)', placa, cam_index)
            if on_plate_callback:
                try:
                    on_plate_callback({
                        'camera': cam_index,
                        'plate': placa,
                        'bbox': list(box),
                        'confidence': conf,
                    })
                except Exception as e:  # noqa: BLE001
                    log.exception('Erro no callback da câmera %s: %s', cam_index, e)

        self.hub.publicar_overlay(cam_index, overlay)

    def list_running(self):
        with self.lock:
            return list(self.running.keys())
