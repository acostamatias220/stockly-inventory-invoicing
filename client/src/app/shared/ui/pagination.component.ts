import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'app-pagination',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="pagination" aria-label="Paginación">
      <span class="muted">{{ totalCount() }} resultados · página {{ page() }} de {{ max() }}</span>
      <div class="pages">
        <button type="button" class="btn btn-ghost btn-sm" [disabled]="page() <= 1" (click)="pageChange.emit(page() - 1)">‹ Anterior</button>
        @for (p of pages(); track p) {
          <button type="button" class="btn btn-sm" [class.btn-primary]="p === page()" [class.btn-ghost]="p !== page()"
            [attr.aria-current]="p === page() ? 'page' : null" (click)="pageChange.emit(p)">{{ p }}</button>
        }
        <button type="button" class="btn btn-ghost btn-sm" [disabled]="page() >= max()" (click)="pageChange.emit(page() + 1)">Siguiente ›</button>
      </div>
    </nav>
  `,
})
export class PaginationComponent {
  readonly page = input.required<number>();
  readonly totalPages = input.required<number>();
  readonly totalCount = input(0);
  readonly pageChange = output<number>();

  protected readonly max = computed(() => Math.max(1, this.totalPages()));

  /** A window of up to 5 page numbers around the current one. */
  protected readonly pages = computed(() => {
    const max = this.max();
    const start = Math.max(1, Math.min(this.page() - 2, max - 4));
    return Array.from({ length: Math.min(5, max) }, (_, i) => start + i);
  });
}
