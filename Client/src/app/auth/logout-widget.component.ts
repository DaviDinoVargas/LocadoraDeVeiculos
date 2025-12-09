// logout-widget.component.ts
import { Component, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, NavigationEnd } from '@angular/router';
import { filter, Subscription } from 'rxjs';
import { AuthService } from './auth.service';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'app-logout-widget',
  standalone: true,
  imports: [CommonModule, MatButtonModule],
  templateUrl: './logout-widget.component.html',
  styleUrls: ['./scss/auth-shared-styles.css']
})
export class LogoutWidgetComponent implements OnDestroy {
  public show = false;
  public usuarioNome: string | null = null;

  private sub?: Subscription;

  // rotas onde o widget ficará oculto
  private hidePrefixes = ['/login', '/registrar'];

  constructor(
    private router: Router,
    private auth: AuthService,
    private snackBar: MatSnackBar
  ) {
    this.updateVisibility(this.router.url);

    this.sub = this.router.events.pipe(
      filter(e => e instanceof NavigationEnd)
    ).subscribe((e: any) => this.updateVisibility(e.urlAfterRedirects ?? e.url));
  }

  private updateVisibility(url: string) {
    const path = (url.split('?')[0] || '').toLowerCase();
    const isHidden = this.hidePrefixes.some(p => path.startsWith(p));
    const logged = this.auth.isLoggedIn();
    this.show = !isHidden && logged;

    const usuario = this.auth.getUsuario();
    this.usuarioNome = usuario?.nomeCompleto ?? null;
  }

  async sair() {
    try {
      await this.auth.sair().toPromise();
      this.snackBar.open('Deslogado com sucesso', 'OK', { duration: 2500 });
      this.router.navigate(['/login']);
    } catch (err) {
      console.error('Erro no logout', err);
      this.snackBar.open('Erro ao deslogar', 'Fechar', { duration: 4000 });
    }
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }
}
