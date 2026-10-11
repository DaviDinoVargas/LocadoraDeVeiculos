import { BreakpointObserver } from '@angular/cdk/layout';
import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, HostListener, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmDialogService } from '@spartan-ng/helm/dialog';
import { HlmDropdownMenuImports } from '@spartan-ng/helm/dropdown-menu';
import { HlmSheetImports } from '@spartan-ng/helm/sheet';
import { map } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { menuParaCargo } from '../../core/navegacao';
import { NotificacaoService } from '../../core/notificacao.service';
import { TemaService } from '../../core/tema.service';
import { ChatbotUiService } from '../../Components/chatbot/chatbot-ui.service';
import { CommandPaletteComponent } from './command-palette.component';
import { SidebarNavComponent } from './sidebar-nav.component';

const CHAVE_RECOLHIDO = 'menu-recolhido';

function lerRecolhido(): boolean {
  try {
    return localStorage.getItem(CHAVE_RECOLHIDO) === '1';
  } catch {
    return false;
  }
}

/**
 * Casca da aplicação (SKILL §8.1): barra lateral (recolhível; trilho de ícones no tablet; gaveta no
 * mobile) + barra superior com busca (Ctrl/Cmd + K), tema e menu do usuário. O widget do usuário
 * deixou de ser flutuante, então não cobre mais os botões de ação das páginas.
 * Em /login e /registrar (`comChrome` falso) só projeta o conteúdo.
 */
@Component({
  selector: 'app-shell',
  imports: [NgIcon, NgTemplateOutlet, SidebarNavComponent, HlmButtonImports, HlmDropdownMenuImports, HlmSheetImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div [class]="comChrome() ? 'bg-background flex h-dvh overflow-hidden' : 'contents'">
      @if (comChrome()) {
        <a
          href="#conteudo"
          class="bg-primary text-primary-foreground sr-only z-50 rounded-full px-4 py-2 focus:not-sr-only focus:fixed focus:top-2 focus:left-2"
        >
          Pular para o conteúdo
        </a>

        <!-- Barra lateral: tablet = trilho de ícones, desktop = completa ou recolhida -->
        <aside
          class="border-border bg-surface-low hidden shrink-0 flex-col border-r transition-[width] duration-200 md:flex"
          [class.w-[72px]]="recolhidoEfetivo()"
          [class.w-[260px]]="!recolhidoEfetivo()"
        >
          <div class="flex h-14 shrink-0 items-center gap-3 px-4" [class.justify-center]="recolhidoEfetivo()">
            <span class="bg-primary text-primary-foreground flex size-9 shrink-0 items-center justify-center rounded-full">
              <ng-icon name="lucideCar" class="text-lg" aria-hidden="true" />
            </span>
            @if (!recolhidoEfetivo()) {
              <span class="text-title-m text-foreground truncate">Locadora de Veículos</span>
            }
          </div>

          <div class="min-h-0 flex-1 overflow-y-auto px-3 py-2">
            <app-sidebar-nav [grupos]="menu()" [recolhido]="recolhidoEfetivo()" />
          </div>

          @if (largo()) {
            <div class="border-border shrink-0 border-t p-3">
              <button
                hlmBtn
                variant="ghost"
                type="button"
                class="w-full justify-start"
                [class.justify-center]="recolhido()"
                [attr.aria-label]="recolhido() ? 'Expandir menu' : 'Recolher menu'"
                (click)="alternarRecolhido()"
              >
                <ng-icon [name]="recolhido() ? 'lucidePanelLeftOpen' : 'lucidePanelLeftClose'" aria-hidden="true" />
                @if (!recolhido()) {
                  <span>Recolher menu</span>
                }
              </button>
            </div>
          }
        </aside>

        <div class="flex min-w-0 flex-1 flex-col">
          <header class="border-border bg-background flex h-14 shrink-0 items-center gap-2 border-b px-3 sm:px-4">
            <button hlmBtn variant="ghost" size="icon" type="button" class="md:hidden" aria-label="Abrir menu" (click)="gaveta.set(true)">
              <ng-icon name="lucideMenu" aria-hidden="true" />
            </button>

            <button
              type="button"
              class="border-input bg-surface-lowest text-body-m text-muted-foreground hover:bg-accent/40 flex h-10 min-w-0 flex-1 items-center gap-2 rounded-full border px-4 sm:max-w-md"
              aria-label="Buscar tela (Ctrl+K)"
              (click)="abrirPaleta()"
            >
              <ng-icon name="lucideSearch" aria-hidden="true" />
              <span class="flex-1 truncate text-left">Buscar tela…</span>
              <kbd class="text-label-s border-border bg-muted hidden rounded-md border px-1.5 py-0.5 sm:inline">Ctrl K</kbd>
            </button>

            <div class="ml-auto flex items-center gap-1">
              <button
                hlmBtn
                variant="ghost"
                size="icon"
                type="button"
                aria-label="Assistente da locadora"
                [attr.aria-pressed]="chatbot.aberto()"
                (click)="chatbot.alternar()"
              >
                <ng-icon name="lucideMessageCircle" aria-hidden="true" />
              </button>

              <button
                hlmBtn
                variant="ghost"
                size="icon"
                type="button"
                [attr.aria-label]="tema.tema() === 'escuro' ? 'Usar tema claro' : 'Usar tema escuro'"
                (click)="tema.alternar()"
              >
                <ng-icon [name]="tema.tema() === 'escuro' ? 'lucideSun' : 'lucideMoon'" aria-hidden="true" />
              </button>

              <button
                type="button"
                class="hover:bg-accent/60 flex min-h-11 items-center gap-2 rounded-full py-1 pr-2 pl-1"
                [hlmDropdownMenuTrigger]="menuUsuario"
                align="end"
                aria-label="Menu do usuário"
              >
                <span class="bg-primary-container text-on-primary-container text-label-m flex size-9 items-center justify-center rounded-full">
                  {{ iniciais() }}
                </span>
                <span class="text-label-l text-foreground hidden max-w-40 truncate sm:inline">{{ nome() }}</span>
                <ng-icon name="lucideChevronDown" class="text-muted-foreground hidden sm:block" aria-hidden="true" />
              </button>
            </div>
          </header>

          <main id="conteudo" tabindex="-1" class="min-h-0 flex-1 overflow-y-auto px-4 py-6 outline-none sm:px-6 lg:px-8">
            <ng-container [ngTemplateOutlet]="conteudo" />
          </main>
        </div>
      } @else {
        <ng-container [ngTemplateOutlet]="conteudo" />
      }
    </div>

    <ng-template #conteudo><ng-content /></ng-template>

    <ng-template #menuUsuario>
      <hlm-dropdown-menu class="w-60">
        <div class="px-2 py-1.5">
          <p class="text-label-l truncate">{{ nome() }}</p>
          <p class="text-body-s text-muted-foreground truncate">{{ email() }}</p>
          <p class="text-body-s text-muted-foreground">{{ cargo() }}</p>
        </div>
        <hlm-dropdown-menu-separator />
        <hlm-dropdown-menu-group>
          <button hlmDropdownMenuItem type="button" (triggered)="irPara('/verificacao-facial')">
            <ng-icon name="lucideScanFace" aria-hidden="true" />
            Verificação facial
          </button>
          <button hlmDropdownMenuItem type="button" variant="destructive" (triggered)="sair()">
            <ng-icon name="lucideLogOut" aria-hidden="true" />
            Sair
          </button>
        </hlm-dropdown-menu-group>
      </hlm-dropdown-menu>
    </ng-template>

    @if (comChrome()) {
      <!-- Gaveta do mobile (< 768 px) -->
      <hlm-sheet side="left" [state]="gaveta() ? 'open' : 'closed'" (stateChanged)="gaveta.set($event === 'open')">
        <hlm-sheet-content *hlmSheetPortal="let ctx" class="w-72! max-w-[85vw]! p-0">
          <div class="flex h-14 shrink-0 items-center gap-3 px-4">
            <span class="bg-primary text-primary-foreground flex size-9 items-center justify-center rounded-full">
              <ng-icon name="lucideCar" class="text-lg" aria-hidden="true" />
            </span>
            <span class="text-title-m">Locadora de Veículos</span>
          </div>
          <div class="min-h-0 flex-1 overflow-y-auto px-3 pb-4">
            <app-sidebar-nav [grupos]="menu()" (navegou)="gaveta.set(false)" />
          </div>
        </hlm-sheet-content>
      </hlm-sheet>

    }
  `
})
export class AppShellComponent {
  /** Falso nas telas de autenticação: sem barra lateral/superior. */
  readonly comChrome = input(true);

  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly dialogos = inject(HlmDialogService);
  private readonly notificacao = inject(NotificacaoService);
  private readonly breakpoints = inject(BreakpointObserver);
  protected readonly tema = inject(TemaService);
  protected readonly chatbot = inject(ChatbotUiService);

  protected readonly gaveta = signal(false);
  protected readonly recolhido = signal(lerRecolhido());

  // >= 1024 px: barra completa/recolhível. 768–1023 px: trilho. < 768 px: gaveta.
  protected readonly largo = toSignal(this.breakpoints.observe('(min-width: 1024px)').pipe(map(r => r.matches)), {
    initialValue: true
  });
  protected readonly recolhidoEfetivo = computed(() => !this.largo() || this.recolhido());

  protected readonly usuario = computed(() => (this.comChrome() ? this.auth.getUsuario() : null));
  protected readonly nome = computed(() => this.usuario()?.nomeCompleto || 'Usuário');
  protected readonly email = computed(() => this.usuario()?.email ?? '');
  protected readonly cargo = computed(() => this.usuario()?.cargo ?? '');
  protected readonly iniciais = computed(() => {
    const partes = this.nome().trim().split(/\s+/).filter(Boolean);
    if (partes.length === 0) return '?';
    if (partes.length === 1) return partes[0].charAt(0).toUpperCase();
    return (partes[0].charAt(0) + partes[partes.length - 1].charAt(0)).toUpperCase();
  });
  protected readonly menu = computed(() => (this.comChrome() ? menuParaCargo(this.usuario()?.cargo) : []));

  @HostListener('document:keydown', ['$event'])
  protected aoTeclar(evento: KeyboardEvent): void {
    if (this.comChrome() && (evento.ctrlKey || evento.metaKey) && evento.key.toLowerCase() === 'k') {
      evento.preventDefault();
      this.abrirPaleta();
    }
  }

  protected alternarRecolhido(): void {
    const novo = !this.recolhido();
    this.recolhido.set(novo);
    try {
      localStorage.setItem(CHAVE_RECOLHIDO, novo ? '1' : '0');
    } catch {
      // armazenamento indisponível: a preferência vale só nesta visita
    }
  }

  protected abrirPaleta(): void {
    this.dialogos.open(CommandPaletteComponent, { showCloseButton: false, contentClass: 'sm:max-w-xl' });
  }

  protected irPara(caminho: string): void {
    void this.router.navigateByUrl(caminho);
  }

  protected sair(): void {
    const finalizar = () => {
      this.chatbot.fechar();
      void this.router.navigate(['/login']);
    };
    this.auth.sair().subscribe({
      next: () => {
        this.notificacao.sucesso('Sessão encerrada');
        finalizar();
      },
      error: () => {
        // sair() já limpou a sessão local mesmo com o backend indisponível
        this.notificacao.info('Sessão encerrada neste dispositivo');
        finalizar();
      }
    });
  }
}
