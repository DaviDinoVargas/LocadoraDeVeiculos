# Dono único de cada câmera física.
#
# Antes, /stream/{id} e /start abriam cada um o seu cv2.VideoCapture para o
# mesmo índice; no Windows a segunda abertura costuma falhar ou travar, e o
# stream nunca mostrava o que o YOLO detectou. Agora uma thread por câmera lê
# os frames e guarda o mais recente; quem precisa de imagem (o loop do YOLO, o
# stream MJPEG, o teste) só "aluga" a câmera com acquire()/release() e lê desse
# frame compartilhado. A câmera abre no primeiro acquire e fecha no último
# release.
import threading
import time
from dataclasses import dataclass, field

# Leituras seguidas sem frame antes de considerar a câmera caída (ex.: webcam
# desconectada). A 10 ms por tentativa são ~3 s.
MAX_FALHAS_SEGUIDAS = 300
# Quanto tempo uma caixa publicada pelo detector continua desenhada no stream.
VALIDADE_OVERLAY_S = 1.5


@dataclass
class _Camera:
    cap: object
    refs: int = 0
    frame: object = None
    seq: int = 0
    vivo: bool = True
    parar: bool = False
    overlay: list = field(default_factory=list)
    overlay_ts: float = 0.0
    cond: threading.Condition = field(default_factory=threading.Condition)
    thread: threading.Thread | None = None


class CameraHub:
    def __init__(self, abrir=None):
        # `abrir(indice)` devolve um objeto estilo cv2.VideoCapture; injetável nos testes.
        if abrir is None:
            from yolo_tesseract.camera_server import open_camera as abrir
        self._abrir = abrir
        self._lock = threading.Lock()
        self._cameras: dict[int, _Camera] = {}
        self._travas_abertura: dict[int, threading.Lock] = {}

    # ---- ciclo de vida -------------------------------------------------
    def acquire(self, indice: int) -> bool:
        with self._lock:
            trava = self._travas_abertura.setdefault(indice, threading.Lock())
        # A abertura de uma webcam pode demorar segundos: fica sob a trava DA CÂMERA (dois pedidos
        # para o mesmo índice abrem uma vez só) e fora do lock global, que as leituras usam.
        with trava:
            with self._lock:
                cam = self._cameras.get(indice)
                if cam is not None and cam.vivo:
                    cam.refs += 1
                    return True
                if cam is not None:  # caiu: descarta e tenta abrir de novo
                    self._encerrar_locked(indice)

            cap = self._abrir(indice)
            if cap is None or not cap.isOpened():
                if cap is not None:
                    try:
                        cap.release()
                    except Exception:
                        pass
                return False

            cam = _Camera(cap=cap, refs=1)
            cam.thread = threading.Thread(target=self._ler, args=(cam,), daemon=True, name=f'camera-{indice}')
            with self._lock:
                self._cameras[indice] = cam
            cam.thread.start()
            return True

    def release(self, indice: int) -> None:
        with self._lock:
            cam = self._cameras.get(indice)
            if cam is None:
                return
            cam.refs -= 1
            if cam.refs <= 0:
                self._encerrar_locked(indice)

    def shutdown(self) -> None:
        with self._lock:
            for indice in list(self._cameras):
                self._encerrar_locked(indice)

    def _encerrar_locked(self, indice: int) -> None:
        cam = self._cameras.pop(indice, None)
        if cam is None:
            return
        cam.parar = True
        with cam.cond:
            cam.vivo = False
            cam.cond.notify_all()
        # A thread de leitura libera o cap ao sair (nunca release() concorrente
        # com um read() em andamento).

    def _ler(self, cam: _Camera) -> None:
        falhas = 0
        try:
            while not cam.parar:
                ok, frame = cam.cap.read()
                if not ok or frame is None:
                    falhas += 1
                    if falhas >= MAX_FALHAS_SEGUIDAS:
                        break
                    time.sleep(0.01)
                    continue
                falhas = 0
                with cam.cond:
                    cam.frame = frame
                    cam.seq += 1
                    cam.cond.notify_all()
        finally:
            with cam.cond:
                cam.vivo = False
                cam.cond.notify_all()
            try:
                cam.cap.release()
            except Exception:
                pass

    # ---- leitura -------------------------------------------------------
    def read(self, indice: int, ultima_seq: int = 0, timeout: float = 1.0):
        """Espera um frame mais novo que `ultima_seq`. Devolve (frame, seq);
        (None, ultima_seq) se estourou o tempo ou a câmera não está aberta."""
        with self._lock:
            cam = self._cameras.get(indice)
        if cam is None:
            return None, ultima_seq
        with cam.cond:
            cam.cond.wait_for(lambda: cam.seq > ultima_seq or not cam.vivo, timeout)
            if cam.seq > ultima_seq and cam.frame is not None:
                return cam.frame.copy(), cam.seq
        return None, ultima_seq

    def esta_aberta(self, indice: int) -> bool:
        with self._lock:
            cam = self._cameras.get(indice)
        return cam is not None and cam.vivo

    def abertas(self) -> list[int]:
        with self._lock:
            return sorted(i for i, c in self._cameras.items() if c.vivo)

    # ---- overlay (caixas desenhadas no stream) -------------------------
    def publicar_overlay(self, indice: int, itens: list) -> None:
        with self._lock:
            cam = self._cameras.get(indice)
        if cam is not None:
            cam.overlay = itens
            cam.overlay_ts = time.monotonic()

    def overlay(self, indice: int) -> list:
        with self._lock:
            cam = self._cameras.get(indice)
        if cam is None or time.monotonic() - cam.overlay_ts > VALIDADE_OVERLAY_S:
            return []
        return cam.overlay
