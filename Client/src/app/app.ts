import { Component, signal, ViewChild } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { LogoutWidgetComponent } from "./auth/logout-widget.component";

import { AuthService } from './auth/auth.service';
import { SidebarComponent } from "./Components/sidebar/sidebar.component";
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { filter } from 'rxjs';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    LogoutWidgetComponent,
    SidebarComponent
  ],
  template: `
    <app-logout-widget></app-logout-widget>
    <div class="app-container">
      <!-- Sidebar só aparece em rotas permitidas -->
      <app-sidebar #sidebar *ngIf="showSidebar"></app-sidebar>
      <main class="app-main" [class.collapsed]="sidebar?.collapsed && !sidebar?.isMobile">
        <router-outlet></router-outlet>
      </main>
    </div>
  `,
  styles: [`
    .app-container {
      display: flex;
      flex-direction: row;
      height: 100vh;
      width: 100%;
      overflow: hidden;
    }

    .app-main {
      flex: 1;
      padding: 16px;
      box-sizing: border-box;
      height: 100%;
      overflow-y: auto; /* Permite scroll vertical se necessário */
      transition: margin-left 180ms ease;
      margin-left: 260px; /* Largura padrão da sidebar */
    }

    /* Ajusta a margem quando a sidebar está colapsada */
    .app-main.collapsed {
      margin-left: 64px;
    }

    @media (max-width: 800px) {
      .app-main {
        margin-left: 0 !important;
        width: 100%;
        padding-top: 72px; /* Espaço para o botão hambúrguer */
      }
    }
  `]
})
export class AppComponent {
  @ViewChild('sidebar') sidebar?: SidebarComponent;

  private hideRoutes = ['/login', '/registrar'];

  constructor(private router: Router, private auth: AuthService) {}

  get showSidebar(): boolean {
    const path = this.router.url.split('?')[0].split('#')[0];
    return !this.hideRoutes.includes(path) && this.auth.isLoggedIn();
  }
}
