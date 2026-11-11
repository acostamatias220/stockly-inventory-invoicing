import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { INVOICE_STATUS_LABELS } from '../../core/labels';
import { InvoiceStatus } from '../../core/models';

@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge" [class]="'badge badge-' + status().toLowerCase()">{{ label() }}</span>
    @if (overdue()) { <span class="badge badge-overdue">Vencida</span> }`,
})
export class StatusBadgeComponent {
  readonly status = input.required<InvoiceStatus>();
  readonly overdue = input(false);
  protected readonly label = computed(() => INVOICE_STATUS_LABELS[this.status()]);
}
