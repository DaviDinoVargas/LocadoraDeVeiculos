import cv2
import numpy as np

def preprocess_image(image_path):
    """
    Carrega e pré-processa uma imagem para análise facial.
    """
    image = cv2.imread(image_path)
    if image is None:
        raise ValueError("Imagem não encontrada ou inválida.")
    
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    return gray

def calculate_similarity(img1, img2):
    """
    Compara duas imagens e retorna um valor de similaridade.
    """
    if img1.shape != img2.shape:
        raise ValueError("As imagens precisam ter o mesmo tamanho para comparação.")
    
    difference = cv2.absdiff(img1, img2)
    similarity = 1 - (cv2.mean(difference)[0] / 255.0)
    return similarity

def resize_image(image, width, height):
    """
    Redimensiona uma imagem para o tamanho especificado.
    """
    return cv2.resize(image, (width, height))
