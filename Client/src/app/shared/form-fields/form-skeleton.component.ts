import { ChangeDetectionStrategy, Component } from '@angular/core';
import { HlmSkeleton } from '@spartan-ng/helm/skeleton';

/** Esqueleto do formulário em edição, enquanto o registro carrega (evita salto de layout). */
@Component({
  selector: 'app-form-skeleton',
  imports: [HlmSkeleton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="border-border bg-card flex flex-col gap-5 rounded-xl border p-6" aria-busy="true" aria-label="Carregando formulário">
      <hlm-skeleton class="block h-5 w-1/3" />
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
        @for (n of campos; track n) {
          <div class="flex flex-col gap-2">
            <hlm-skeleton class="block h-4 w-24" />
            <hlm-skeleton class="block h-10 w-full" />
          </div>
        }
      </div>
    </div>
  `
})
export class FormSkeletonComponent {
  protected readonly campos = [0, 1, 2, 3];
}
