import { ChangeDetectionStrategy, Component, ElementRef, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { BrnDialogRef } from '@spartan-ng/brain/dialog';
import { AuthService } from '../../auth/auth.service';
import { menuParaCargo } from '../../core/navegacao';

interface Destino {
  rotulo: string;
  grupo: string;
  icone: string;
  caminho: string;
}

/** Paleta de comandos (Ctrl/Cmd + K): busca por tela e navega pelo teclado (↑ ↓ Enter, Esc fecha). */
@Component({
  selector: 'app-command-palette',
  imports: [NgIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="-m-6 flex flex-col">
      <div class="border-border flex items-center gap-2 border-b px-4">
        <ng-icon name="lucideSearch" class="text-muted-foreground text-lg" aria-hidden="true" />
        <input
          #campo
          type="text"
          role="combobox"
          aria-expanded="true"
          aria-controls="paleta-lista"
          class="text-body-l placeholder:text-muted-foreground h-12 w-full bg-transparent outline-none"
          placeholder="Ir para… (clientes, veículos, aluguéis)"
          aria-label="Buscar tela"
          [value]="consulta()"
          (input)="consulta.set(campo.value); indice.set(0)"
          (keydown)="teclado($event)"
        />
      </div>
      <ul id="paleta-lista" role="listbox" class="max-h-80 overflow-y-auto p-2">
        @for (d of filtrados(); track d.caminho; let i = $index) {
          <li role="option" [attr.aria-selected]="i === indice()">
            <button
              type="button"
              class="text-body-m flex min-h-11 w-full items-center gap-3 rounded-lg px-3 text-left"
              [class.bg-accent]="i === indice()"
              [class.text-accent-foreground]="i === indice()"
              (mouseenter)="indice.set(i)"
              (click)="ir(d)"
            >
              <ng-icon [name]="d.icone" class="text-lg" aria-hidden="true" />
              <span class="flex-1">{{ d.rotulo }}</span>
              <span class="text-body-s text-muted-foreground">{{ d.grupo }}</span>
            </button>
          </li>
        } @empty {
          <li class="text-body-m text-muted-foreground px-3 py-6 text-center">Nenhuma tela encontrada.</li>
        }
      </ul>
    </div>
  `
})
export class CommandPaletteComponent {
  private readonly router = inject(Router);
  private readonly ref = inject<BrnDialogRef<void>>(BrnDialogRef);
  private readonly campo = viewChild<ElementRef<HTMLInputElement>>('campo');

  protected readonly consulta = signal('');
  protected readonly indice = signal(0);

  private readonly destinos: Destino[] = menuParaCargo(inject(AuthService).getUsuario()?.cargo).flatMap(grupo =>
    grupo.itens.flatMap(item =>
      item.filhos
        ? item.filhos.map(f => ({ rotulo: f.rotulo, grupo: item.rotulo, icone: f.icone, caminho: f.caminho }))
        : [{ rotulo: item.rotulo, grupo: grupo.rotulo, icone: item.icone, caminho: item.caminho }]
    )
  );

  protected readonly filtrados = computed(() => {
    const termo = this.consulta().trim().toLowerCase();
    if (!termo) return this.destinos;
    return this.destinos.filter(d => `${d.rotulo} ${d.grupo}`.toLowerCase().includes(termo));
  });

  constructor() {
    afterNextRender(() => this.campo()?.nativeElement.focus());
  }

  protected teclado(evento: KeyboardEvent): void {
    const total = this.filtrados().length;
    if (evento.key === 'ArrowDown') {
      evento.preventDefault();
      this.indice.update(i => (total ? (i + 1) % total : 0));
    } else if (evento.key === 'ArrowUp') {
      evento.preventDefault();
      this.indice.update(i => (total ? (i - 1 + total) % total : 0));
    } else if (evento.key === 'Enter') {
      evento.preventDefault();
      const escolhido = this.filtrados()[this.indice()];
      if (escolhido) this.ir(escolhido);
    }
  }

  protected ir(destino: Destino): void {
    this.ref.close();
    const [caminho, consulta] = destino.caminho.split('?');
    void this.router.navigate([caminho], {
      queryParams: consulta ? Object.fromEntries(new URLSearchParams(consulta)) : undefined
    });
  }
}
