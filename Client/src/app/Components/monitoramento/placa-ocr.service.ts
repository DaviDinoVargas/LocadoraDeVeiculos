import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

export interface DetectedPlate {
  id: string;
  plateNumber: string;
  timestamp: Date;
  cameraId: number;
  confidence: number;
  imageData?: string;
}

@Injectable({ providedIn: 'root' })
export class PlacaOcrService {
  private detectedPlatesSubject = new BehaviorSubject<DetectedPlate[]>([]);
  private selectedPlateSubject = new BehaviorSubject<DetectedPlate | null>(null);

  detectedPlates$ = this.detectedPlatesSubject.asObservable();
  selectedPlate$ = this.selectedPlateSubject.asObservable();

  addDetectedPlate(plate: DetectedPlate) {
    const currentPlates = this.detectedPlatesSubject.value;

    // Evitar duplicatas recentes (últimos 5 segundos)
    const now = new Date();
    const recentCutoff = new Date(now.getTime() - 5000);

    const isDuplicate = currentPlates.some(p =>
      p.plateNumber === plate.plateNumber &&
      new Date(p.timestamp) > recentCutoff
    );

    if (!isDuplicate) {
      const updatedPlates = [plate, ...currentPlates.slice(0, 9)]; // Mantém apenas as 10 mais recentes
      this.detectedPlatesSubject.next(updatedPlates);
    }
  }

  selectPlate(plate: DetectedPlate) {
    this.selectedPlateSubject.next(plate);
  }

  clearSelectedPlate() {
    this.selectedPlateSubject.next(null);
  }

  removePlate(plateId: string) {
    const updatedPlates = this.detectedPlatesSubject.value.filter(p => p.id !== plateId);
    this.detectedPlatesSubject.next(updatedPlates);
  }

  clearAllPlates() {
    this.detectedPlatesSubject.next([]);
    this.clearSelectedPlate();
  }
}
