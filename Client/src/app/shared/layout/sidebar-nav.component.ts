import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { HlmTooltipImports } from '@spartan-ng/helm/tooltip';
import { filter, map, startWith } from 'rxjs';
import { GrupoMenu, ItemMenu } from '../../core/navegacao';

/**
 * Lista de navegação do app (usada na barra lateral do desktop e na gaveta do mobile).
 * Cada grupo (Gestão, Operacional...) mostra o rótulo UMA vez; itens com subitens expandem/recolhem.
 */
@Component({
  selector: 'app-sidebar-nav',
  imports: [RouterLink, NgIcon, HlmTooltipImports],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav aria-label="Navegação principal" class="flex flex-col gap-4">
      @for (grupo of grupos(); track grupo.rotulo) {
        <section class="flex flex-col gap-1">
          @if (!recolhido()) {
            <h2 class="text-label-s text-muted-foreground px-3 pt-1 tracking-wide uppercase">{{ grupo.rotulo }}</h2>
          } @else {
            <div class="bg-border mx-3 my-1 h-px" aria-hidden="true"></div>
          }
          <ul class="flex flex-col gap-0.5">
            @for (item of grupo.itens; track item.rotulo) {
              <li>
                @if (item.filhos && !recolhido()) {
                  <button
                    type="button"
                    class="text-label-l hover:bg-accent/60 flex min-h-11 w-full items-center gap-3 rounded-full px-3 text-left transition-colors"
                    [class.text-foreground]="itemAtivo(item)"
                    [class.text-muted-foreground]="!itemAtivo(item)"
                    [attr.aria-expanded]="expandido(item)"
                    (click)="alternar(item)"
                  >
                    <ng-icon [name]="item.icone" class="text-lg" aria-hidden="true" />
                    <span class="flex-1 truncate">{{ item.rotulo }}</span>
                    <ng-icon
                      name="lucideChevronDown"
                      class="text-base transition-transform"
                      [class.rotate-180]="expandido(item)"
                      aria-hidden="true"
                    />
                  </button>
                  @if (expandido(item)) {
                    <ul class="border-border mt-0.5 ml-6 flex flex-col gap-0.5 border-l pl-3">
                      @for (filho of item.filhos; track filho.caminho) {
                        <li>
                          <a
                            [routerLink]="comoRota(filho.caminho)"
                            [queryParams]="consulta(filho.caminho)"
                            class="text-label-l hover:bg-accent/60 flex min-h-10 items-center gap-2 rounded-full px-3 transition-colors"
                            [class.bg-secondary]="filhoAtivo(item, filho)"
                            [class.text-secondary-foreground]="filhoAtivo(item, filho)"
                            [class.text-muted-foreground]="!filhoAtivo(item, filho)"
                            [attr.aria-current]="filhoAtivo(item, filho) ? 'page' : null"
                            (click)="navegou.emit()"
                          >
                            <ng-icon [name]="filho.icone" class="text-base" aria-hidden="true" />
                            <span class="truncate">{{ filho.rotulo }}</span>
                          </a>
                        </li>
                      }
                    </ul>
                  }
                } @else {
                  <a
                    [routerLink]="comoRota(item.caminho)"
                    [queryParams]="consulta(item.caminho)"
                    class="text-label-l hover:bg-accent/60 flex min-h-11 items-center gap-3 rounded-full px-3 transition-colors"
                    [class.justify-center]="recolhido()"
                    [class.bg-secondary]="itemAtivo(item)"
                    [class.text-secondary-foreground]="itemAtivo(item)"
                    [class.text-muted-foreground]="!itemAtivo(item)"
                    [attr.aria-current]="itemAtivo(item) ? 'page' : null"
                    [attr.aria-label]="recolhido() ? item.rotulo : null"
                    [hlmTooltip]="recolhido() ? item.rotulo : ''"
                    position="right"
                    (click)="navegou.emit()"
                  >
                    <ng-icon [name]="item.icone" class="text-lg" aria-hidden="true" />
                    @if (!recolhido()) {
                      <span class="truncate">{{ item.rotulo }}</span>
                    }
                  </a>
                }
              </li>
            }
          </ul>
        </section>
      }
    </nav>
  `
})
export class SidebarNavComponent {
  readonly grupos = input.required<GrupoMenu[]>();
  /** Só ícones (trilho): sem rótulos, sem subitens. */
  readonly recolhido = input(false);
  readonly navegou = output<void>();

  private readonly router = inject(Router);
  private readonly aberto = signal<Record<string, boolean>>({});

  private readonly url = toSignal(
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      map(() => this.router.url),
      startWith(this.router.url)
    ),
    { initialValue: this.router.url }
  );

  private readonly partes = computed(() => {
    const [caminho, consulta = ''] = this.url().split('?');
    return { caminho, consulta };
  });

  protected comoRota(caminho: string): string {
    return caminho.split('?')[0];
  }

  protected consulta(caminho: string): Record<string, string> | undefined {
    const consulta = caminho.split('?')[1];
    return consulta ? Object.fromEntries(new URLSearchParams(consulta)) : undefined;
  }

  protected expandido(item: ItemMenu): boolean {
    return this.aberto()[item.rotulo] ?? this.itemAtivo(item);
  }

  protected alternar(item: ItemMenu): void {
    this.aberto.update(atual => ({ ...atual, [item.rotulo]: !this.expandido(item) }));
  }

  /** O item (ou algum subitem) corresponde à rota atual. */
  protected itemAtivo(item: ItemMenu): boolean {
    if (item.filhos) {
      return item.filhos.some(filho => this.filhoAtivo(item, filho)) || this.naRaiz(item.caminho);
    }
    return this.naRaiz(item.caminho);
  }

  protected filhoAtivo(pai: ItemMenu, filho: ItemMenu): boolean {
    const [base, consulta] = filho.caminho.split('?');
    const { caminho, consulta: consultaAtual } = this.partes();
    if (consulta) {
      return caminho === base && consultaAtual === consulta;
    }
    // O item "Todos" não fica ativo quando um irmão com filtro (?status=...) é que casa.
    const irmaoComFiltro = pai.filhos?.some(f => f !== filho && f.caminho.startsWith(`${base}?`));
    if (irmaoComFiltro && consultaAtual) return false;
    return caminho === base;
  }

  private naRaiz(caminho: string): boolean {
    const base = caminho.split('?')[0];
    const atual = this.partes().caminho;
    return atual === base || atual.startsWith(`${base}/`);
  }
}
