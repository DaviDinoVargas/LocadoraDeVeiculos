import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, NavigationEnd } from '@angular/router';
import { filter, Subscription } from 'rxjs';
import { AuthService } from './auth.service';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatIcon } from "@angular/material/icon";

@Component({
  selector: 'app-logout-widget',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatSnackBarModule, MatIcon],
  templateUrl: './logout-widget.component.html',
  styleUrls: ['./scss/auth-shared-styles.css']
})
export class LogoutWidgetComponent implements OnInit, OnDestroy {
  public show = false;
  public usuarioNome: string | null = null;

  private sub?: Subscription;

  // rotas onde o widget ficará oculto
  private hidePrefixes = ['/login', '/registrar'];

  constructor(
    private router: Router,
    private auth: AuthService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    // Inicializa com o estado atual
    this.updateVisibilityAndUser();

    // Atualiza em navegações
    this.sub = this.router.events.pipe(
      filter(e => e instanceof NavigationEnd)
    ).subscribe((e: any) => {
      this.updateVisibilityAndUser();
    });
  }

  private updateVisibilityAndUser(): void {
    const path = (this.router.url.split('?')[0] || '').toLowerCase();
    const isHidden = this.hidePrefixes.some(p => path.startsWith(p));
    const logged = this.auth.isLoggedIn();

    this.show = !isHidden && logged;

    if (logged) {
      const usuario = this.auth.getUsuario();
      this.usuarioNome = usuario?.nomeCompleto || 'Usuário';
    } else {
      this.usuarioNome = null;
    }
  }

  sair(): void {
    this.auth.sair().subscribe({
      next: () => {
        this.snackBar.open('Deslogado com sucesso', 'OK', {
          duration: 2500,
          horizontalPosition: 'right',
          verticalPosition: 'top'
        });
        this.show = false;
        this.usuarioNome = null;
        this.router.navigate(['/login']);
      },
      error: (err) => {
        console.error('Erro no logout:', err);

        // Mesmo se o backend falhar, limpa o local storage
        this.auth.limparStorage();
        this.show = false;
        this.usuarioNome = null;

        this.snackBar.open('Sessão encerrada', 'Fechar', {
          duration: 3000,
          horizontalPosition: 'right',
          verticalPosition: 'top'
        });

        this.router.navigate(['/login']);
      }
    });
  }

  ngOnDestroy(): void {
    if (this.sub) {
      this.sub.unsubscribe();
    }
  }
}
