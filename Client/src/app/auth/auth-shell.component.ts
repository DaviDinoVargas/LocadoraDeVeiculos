import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-auth-shell',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './auth-shell.component.html',
  styleUrls: ['./auth-shell.component.scss']
})
export class AuthShellComponent {
  @Input() titulo = 'Gerencie sua locadora com simplicidade';
  @Input() destaques: string[] = [
    'Frota, clientes e aluguéis em um só lugar',
    'Leitura automática de placas por câmera',
    'Login e verificação por reconhecimento facial'
  ];
}
