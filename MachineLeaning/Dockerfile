# Build a partir da pasta MachineLeaning/: docker build -f MachineLeaning/Dockerfile MachineLeaning/
FROM python:3.11-slim

WORKDIR /app

# libgl1/libglib2.0-0: dependências de sistema do OpenCV (cv2) que o pacote
# opencv-python não traz sozinho. tesseract-ocr: pytesseract só embrulha o
# binário da linha de comando, ele não vem junto com o pip install.
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgl1 \
    libglib2.0-0 \
    tesseract-ocr \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# O usuário não-root precisa gravar em /app (a ultralytics baixa o yolov8n.pt, modelo genérico
# pré-treinado, para o diretório atual no primeiro uso) e no volume dos rostos cadastrados
# (/data/faces, ver FACE_DATA_DIR no docker-compose.yml). Criar o diretório aqui faz o volume
# nomeado herdar o dono correto.
RUN useradd --create-home appuser     && mkdir -p /data/faces     && chown -R appuser:appuser /app /data/faces
USER appuser

EXPOSE 8000
CMD ["python", "-m", "uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
