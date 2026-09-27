import { Component, OnDestroy, OnInit, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, NavigationEnd } from '@angular/router';
import { filter, Subscription } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { ChatbotService, ChatMensagem } from './chatbot.service';

@Component({
  selector: 'app-chatbot-widget',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chatbot-widget.component.html',
  styleUrls: ['./chatbot-widget.component.scss']
})
export class ChatbotWidgetComponent implements OnInit, OnDestroy, AfterViewChecked {
  @ViewChild('historico') historicoRef?: ElementRef<HTMLDivElement>;

  show = false;
  aberto = false;
  enviando = false;
  mensagemAtual = '';
  mensagens: ChatMensagem[] = [
    { autor: 'bot', texto: 'Olá! Sou o assistente da locadora. Pergunte sobre disponibilidade de veículos, aluguéis em aberto ou uma placa específica.' }
  ];

  private hidePrefixes = ['/login', '/registrar'];
  private sub?: Subscription;
  private deveRolarParaFim = false;

  constructor(
    private router: Router,
    private auth: AuthService,
    private chatbot: ChatbotService
  ) {}

  ngOnInit(): void {
    this.atualizarVisibilidade();
    this.sub = this.router.events.pipe(
      filter(e => e instanceof NavigationEnd)
    ).subscribe(() => this.atualizarVisibilidade());
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  ngAfterViewChecked(): void {
    if (this.deveRolarParaFim && this.historicoRef) {
      this.historicoRef.nativeElement.scrollTop = this.historicoRef.nativeElement.scrollHeight;
      this.deveRolarParaFim = false;
    }
  }

  private atualizarVisibilidade(): void {
    const path = (this.router.url.split('?')[0] || '').toLowerCase();
    const isHidden = this.hidePrefixes.some(p => path.startsWith(p));
    this.show = !isHidden && this.auth.isLoggedIn();
  }

  toggle(): void {
    this.aberto = !this.aberto;
    if (this.aberto) {
      this.deveRolarParaFim = true;
    }
  }

  enviar(): void {
    const texto = this.mensagemAtual.trim();
    if (!texto || this.enviando) {
      return;
    }

    this.mensagens.push({ autor: 'usuario', texto });
    this.mensagemAtual = '';
    this.enviando = true;
    this.deveRolarParaFim = true;

    this.chatbot.enviarMensagem(texto).subscribe({
      next: (resposta) => {
        this.mensagens.push({ autor: 'bot', texto: resposta });
        this.enviando = false;
        this.deveRolarParaFim = true;
      },
      error: () => {
        this.mensagens.push({ autor: 'bot', texto: 'Não consegui falar com o assistente agora. Tente novamente em instantes.' });
        this.enviando = false;
        this.deveRolarParaFim = true;
      }
    });
  }
}
