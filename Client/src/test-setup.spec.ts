import { TestBed } from '@angular/core/testing';
import { provideIcons } from '@ng-icons/core';
import { ICONES } from './app/core/icones';

// Em todo spec: os ícones Lucide registrados (como em app.config), para os componentes renderizarem de verdade.
beforeEach(() => {
  TestBed.configureTestingModule({ providers: [provideIcons(ICONES)] });
});
