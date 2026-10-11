import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NgIcon } from '@ng-icons/core';

/**
 * Moldura das telas de login e cadastro: formulário no canto direito e painel da marca ocupando o
 * resto. A moldura tem a altura exata da janela (h-dvh) e NÃO rola no desktop; só o painel do
 * formulário rola, e só se o conteúdo não couber (telas muito baixas). No mobile o painel da marca
 * some e o logo vai para o topo do formulário.
 */
@Component({
  selector: 'app-auth-shell',
  imports: [NgIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bg-background grid h-dvh grid-cols-1 overflow-hidden lg:grid-cols-[minmax(0,1fr)_minmax(26rem,32rem)]">
      <section
        class="bg-primary text-primary-foreground relative hidden min-h-0 items-center justify-center overflow-hidden p-12 lg:flex xl:p-16"
        aria-label="Apresentação do sistema"
      >
        <div class="relative z-10 flex max-w-xl flex-col gap-8">
          <div class="flex items-center gap-3">
            <span class="bg-primary-foreground/15 flex size-11 items-center justify-center rounded-full">
              <ng-icon name="lucideCar" class="text-2xl" aria-hidden="true" />
            </span>
            <span class="text-title-l">Locadora de Veículos</span>
          </div>

          <h1 class="text-headline-l xl:text-[2.5rem] xl:leading-[3rem]">{{ titulo() }}</h1>

          <ul class="flex flex-col gap-3">
            @for (item of destaques(); track item) {
              <li class="text-body-l flex items-center gap-3">
                <span class="bg-primary-foreground/15 flex size-6 shrink-0 items-center justify-center rounded-full">
                  <ng-icon name="lucideCheck" class="text-sm" aria-hidden="true" />
                </span>
                {{ item }}
              </li>
            }
          </ul>
        </div>

        <!-- formas decorativas, só opacidade do próprio token -->
        <div class="bg-primary-foreground/10 absolute -top-24 -right-24 size-80 rounded-full" aria-hidden="true"></div>
        <div class="bg-primary-foreground/10 absolute -bottom-32 -left-16 size-96 rounded-full" aria-hidden="true"></div>
        <div class="bg-primary-foreground/5 absolute right-1/4 bottom-12 size-40 rounded-full" aria-hidden="true"></div>
      </section>

      <section class="flex min-h-0 flex-col overflow-y-auto">
        <div class="m-auto w-full max-w-[25rem] px-6 py-8">
          <div class="mb-8 flex items-center gap-3 lg:hidden">
            <span class="bg-primary text-primary-foreground flex size-10 items-center justify-center rounded-full">
              <ng-icon name="lucideCar" class="text-xl" aria-hidden="true" />
            </span>
            <span class="text-title-l">Locadora de Veículos</span>
          </div>
          <ng-content />
        </div>
      </section>
    </div>
  `
})
export class AuthShellComponent {
  readonly titulo = input('Gerencie sua locadora com simplicidade');
  readonly destaques = input<string[]>([
    'Frota, clientes e aluguéis em um só lugar',
    'Leitura automática de placas por câmera',
    'Login e verificação por reconhecimento facial'
  ]);
}
