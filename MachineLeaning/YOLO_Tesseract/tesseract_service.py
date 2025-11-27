import pytesseract
import cv2
import numpy as np
from config import OCR_LANG

def ocr_plate_image(bgr_image):
    # converte para gray, aplica thresholding para melhorar OCR
    gray = cv2.cvtColor(bgr_image, cv2.COLOR_BGR2GRAY)
    # equalize + bilateral filter
    gray = cv2.bilateralFilter(gray, 9, 75, 75)
    # tenta adaptive threshold
    th = cv2.adaptiveThreshold(gray, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
    cv2.THRESH_BINARY, 11, 2)
    # pytesseract espera RGB or PIL image
    img_rgb = cv2.cvtColor(th, cv2.COLOR_GRAY2RGB)
    config = f"--psm 7 -c tessedit_char_whitelist=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
    text = pytesseract.image_to_string(img_rgb, lang=OCR_LANG, config=config)
    # clean text
    text = ''.join([c for c in text if c.isalnum()])
    return text