import { ChangeDetectionStrategy, Component, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon } from '@ng-icons/core';
import { HlmButtonImports } from '@spartan-ng/helm/button';
import { HlmInputImports } from '@spartan-ng/helm/input';
import { AuthService } from '../../auth/auth.service';
import { ChatbotUiService } from './chatbot-ui.service';
import { ChatMensagem, ChatbotService } from './chatbot.service';

/**
 * Painel do assistente da locadora (chatbot de regras que consulta a API). O botão que abre fica na barra
 * superior da casca; aqui só vive o painel (diálogo não modal, ancorado no canto superior direito), que
 * fecha com Esc e anuncia as respostas por `aria-live`.
 */
@Component({
  selector: 'app-chatbot-widget',
  imports: [FormsModule, NgIcon, HlmButtonImports, HlmInputImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'ui.fechar()' },
  template: `
    @if (visivel()) {
      <section
        role="dialog"
        aria-label="Assistente da locadora"
        class="border-border bg-surface-high fixed top-16 right-4 z-40 flex h-[min(34rem,calc(100dvh-5rem))] w-[min(24rem,calc(100vw-2rem))] flex-col overflow-hidden rounded-[28px] border shadow-lg"
      >
        <header class="border-border flex items-center justify-between gap-2 border-b px-5 py-3">
          <h2 class="text-title-m flex items-center gap-2">
            <ng-icon name="lucideSparkles" class="text-primary" aria-hidden="true" />
            Assistente da locadora
          </h2>
          <button hlmBtn variant="ghost" size="icon-sm" type="button" aria-label="Fechar assistente" (click)="ui.fechar()">
            <ng-icon name="lucideX" aria-hidden="true" />
          </button>
        </header>

        <div #historico class="flex min-h-0 flex-1 flex-col gap-2 overflow-y-auto px-4 py-3" role="log" aria-live="polite">
          @for (msg of mensagens(); track $index) {
            <p
              class="text-body-m max-w-[85%] rounded-2xl px-3.5 py-2 whitespace-pre-wrap"
              [class]="msg.autor === 'usuario' ? 'bg-primary text-primary-foreground self-end rounded-br-md' : 'bg-secondary text-secondary-foreground self-start rounded-bl-md'"
            >
              {{ msg.texto }}
            </p>
          }
          @if (enviando()) {
            <p class="bg-secondary text-secondary-foreground text-body-m self-start rounded-2xl rounded-bl-md px-3.5 py-2" role="status">
              Digitando…
            </p>
          }
        </div>

        <form class="border-border flex items-center gap-2 border-t p-3" (ngSubmit)="enviar()">
          <input
            hlmInput
            type="text"
            name="mensagem"
            class="rounded-full"
            [(ngModel)]="mensagemAtual"
            placeholder="Digite sua pergunta…"
            aria-label="Mensagem para o assistente"
            autocomplete="off"
            [disabled]="enviando()"
          />
          <button hlmBtn size="icon" type="submit" aria-label="Enviar mensagem" [disabled]="enviando() || !mensagemAtual.trim()">
            <ng-icon name="lucideSend" aria-hidden="true" />
          </button>
        </form>
      </section>
    }
  `
})
export class ChatbotWidgetComponent {
  protected readonly ui = inject(ChatbotUiService);
  private readonly auth = inject(AuthService);
  private readonly chatbot = inject(ChatbotService);
  private readonly historico = viewChild<ElementRef<HTMLDivElement>>('historico');

  readonly enviando = signal(false);
  mensagemAtual = '';
  readonly mensagens = signal<ChatMensagem[]>([
    { autor: 'bot', texto: 'Olá! Sou o assistente da locadora. Pergunte sobre disponibilidade de veículos, aluguéis em aberto ou uma placa específica.' }
  ]);

  protected readonly visivel = computed(() => this.ui.aberto() && this.auth.isLoggedIn());

  constructor() {
    // sempre mostra a última mensagem
    effect(() => {
      this.mensagens();
      this.enviando();
      queueMicrotask(() => {
        const el = this.historico()?.nativeElement;
        if (el) el.scrollTop = el.scrollHeight;
      });
    });
  }

  enviar(): void {
    const texto = this.mensagemAtual.trim();
    if (!texto || this.enviando()) {
      return;
    }

    this.mensagens.update(m => [...m, { autor: 'usuario', texto }]);
    this.mensagemAtual = '';
    this.enviando.set(true);

    this.chatbot.enviarMensagem(texto).subscribe({
      next: resposta => {
        this.mensagens.update(m => [...m, { autor: 'bot', texto: resposta }]);
        this.enviando.set(false);
      },
      error: () => {
        this.mensagens.update(m => [...m, { autor: 'bot', texto: 'Não consegui falar com o assistente agora. Tente novamente em instantes.' }]);
        this.enviando.set(false);
      }
    });
  }
}
