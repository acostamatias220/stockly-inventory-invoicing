import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

/** Clickable column header showing the current sort direction (used inside a <th>). */
@Component({
  selector: 'app-sort-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button type="button" class="sort-btn" [class.active]="direction()" (click)="sortChange.emit(key())"
      [attr.aria-label]="'Ordenar por ' + label()">
      <ng-content />
      <span class="arrow" aria-hidden="true">{{ direction() === 'asc' ? '▲' : direction() === 'desc' ? '▼' : '↕' }}</span>
    </button>
  `,
})
export class SortButtonComponent {
  readonly key = input.required<string>();
  readonly label = input('');
  readonly sort = input<string | null>(null);
  readonly sortChange = output<string>();

  protected readonly direction = computed(() => {
    const sort = this.sort();
    if (sort === this.key()) return 'asc';
    if (sort === `-${this.key()}`) return 'desc';
    return null;
  });
}
