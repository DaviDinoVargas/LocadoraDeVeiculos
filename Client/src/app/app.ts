import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { HlmToaster } from '@spartan-ng/helm/sonner';
import { filter, map, startWith } from 'rxjs';
import { AuthService } from './auth/auth.service';
import { ChatbotWidgetComponent } from './Components/chatbot/chatbot-widget.component';
import { TemaService } from './core/tema.service';
import { AppShellComponent } from './shared/layout/app-shell.component';

// Telas sem barra lateral/superior (autenticação)
const ROTAS_SEM_CHROME = ['/login', '/registrar'];

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, AppShellComponent, ChatbotWidgetComponent, HlmToaster],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-shell [comChrome]="comChrome()">
      <router-outlet />
    </app-shell>
    <app-chatbot-widget />
    <hlm-toaster position="top-right" [theme]="tema.tema() === 'escuro' ? 'dark' : 'light'" richColors closeButton />
  `
})
export class AppComponent {
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  protected readonly tema = inject(TemaService);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      map(() => this.router.url),
      startWith(this.router.url)
    ),
    { initialValue: this.router.url }
  );

  /** Com sessão ativa e fora das telas de autenticação, mostra a casca completa. */
  protected readonly comChrome = computed(() => {
    const caminho = this.url().split('?')[0].split('#')[0];
    return !ROTAS_SEM_CHROME.includes(caminho) && this.auth.isLoggedIn();
  });
}
