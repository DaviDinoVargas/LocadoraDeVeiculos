import { TestBed } from '@angular/core/testing';
import { provideIcons } from '@ng-icons/core';
import { ICONES } from './app/core/icones';

// Em todo spec: os ícones Lucide registrados (como em app.config), para os componentes renderizarem de verdade.
const registrarIcones = () => TestBed.configureTestingModule({ providers: [provideIcons(ICONES)] });

beforeEach(registrarIcones);

// Specs que chamam TestBed.resetTestingModule() no meio do teste perdiam o provedor acima e o NgIcon
// imprimia "No icon named ..." (ruído). Depois de cada reset explícito, os ícones voltam a ser registrados.
const resetOriginal = TestBed.resetTestingModule.bind(TestBed);
TestBed.resetTestingModule = () => {
  const resultado = resetOriginal();
  registrarIcones();
  return resultado;
};
