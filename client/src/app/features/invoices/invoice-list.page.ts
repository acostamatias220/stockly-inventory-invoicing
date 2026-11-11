import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth/auth.service';
import { INVOICE_STATUS_LABELS, entries } from '../../core/labels';
import { InvoiceListItem, InvoiceStatus, Paged } from '../../core/models';
import { ArsPipe } from '../../shared/ui/ars.pipe';
import { ListQuery } from '../../shared/ui/list-query';
import { PaginationComponent } from '../../shared/ui/pagination.component';
import { SortButtonComponent } from '../../shared/ui/sort-button.component';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';

interface InvoiceFilters extends Record<string, string | number | boolean | null> {
  status: InvoiceStatus | null;
  customerId: number | null;
  from: string | null;
  to: string | null;
  overdue: boolean | null;
}

@Component({
  selector: 'app-invoice-list-page',
  imports: [RouterLink, DatePipe, ArsPipe, PaginationComponent, SortButtonComponent, StatusBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <div>
        <h1>Facturas</h1>
        <p class="muted">Borradores, emitidas, cobradas y anuladas</p>
      </div>
      <div class="actions">
        <button type="button" class="btn btn-ghost" (click)="exportCsv()">⇩ Exportar CSV</button>
        @if (canSell) { <a class="btn btn-primary" routerLink="nueva">+ Nueva factura</a> }
      </div>
    </header>

    <section class="card">
      <div class="tabs" role="tablist">
        <button type="button" role="tab" [class.active]="!query.filters().status && !query.filters().overdue" (click)="setStatus(null)">Todas</button>
        @for (s of statuses; track s.value) {
          <button type="button" role="tab" [class.active]="query.filters().status === s.value" (click)="setStatus(s.value)">{{ s.label }}</button>
        }
        <button type="button" role="tab" class="tab-danger" [class.active]="query.filters().overdue" (click)="setOverdue()">Vencidas</button>
      </div>
      <div class="toolbar">
        <input type="search" class="search" placeholder="Número (A-0001-00000012), cliente o CUIT…" aria-label="Buscar facturas"
          [value]="query.search()" (input)="query.setSearch($any($event.target).value)" />
        <label class="inline-field">Emitidas desde <input type="date" (change)="query.setFilter('from', $any($event.target).value || null)" /></label>
        <label class="inline-field">hasta <input type="date" (change)="query.setFilter('to', $any($event.target).value || null)" /></label>
        @if (query.filters().customerId) {
          <button type="button" class="chip removable" (click)="query.setFilter('customerId', null)">Cliente #{{ query.filters().customerId }} ✕</button>
        }
      </div>

      <div class="table-wrap" [class.loading]="invoices.isLoading()">
        <table class="table">
          <thead>
            <tr>
              <th><app-sort-button key="number" label="número" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Número</app-sort-button></th>
              <th><app-sort-button key="customer" label="cliente" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Cliente</app-sort-button></th>
              <th><app-sort-button key="issueDate" label="emisión" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Emisión</app-sort-button></th>
              <th><app-sort-button key="dueDate" label="vencimiento" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Vencimiento</app-sort-button></th>
              <th><app-sort-button key="status" label="estado" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Estado</app-sort-button></th>
              <th class="num"><app-sort-button key="total" label="total" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Total</app-sort-button></th>
            </tr>
          </thead>
          <tbody>
            @for (i of invoices.value()?.items ?? []; track i.id) {
              <tr>
                <td>
                  <a [routerLink]="[i.id]" class="mono">{{ i.number ?? 'Borrador #' + i.id }}</a>
                </td>
                <td>{{ i.customerName }}</td>
                <td>{{ i.issueDate ? (i.issueDate | date: 'dd/MM/yyyy' : 'UTC') : '—' }}</td>
                <td [class.danger]="i.isOverdue">{{ i.dueDate | date: 'dd/MM/yyyy' : 'UTC' }}</td>
                <td><app-status-badge [status]="i.status" [overdue]="i.isOverdue" /></td>
                <td class="num"><strong>{{ i.total | ars }}</strong></td>
              </tr>
            } @empty { <tr><td colspan="6" class="empty">No hay facturas para los filtros elegidos.</td></tr> }
          </tbody>
        </table>
      </div>
      @if (invoices.value(); as page) {
        <app-pagination [page]="page.page" [totalPages]="page.totalPages" [totalCount]="page.totalCount" (pageChange)="query.page.set($event)" />
      }
    </section>
  `,
})
export class InvoiceListPage implements OnInit {
  private readonly api = inject(ApiService);
  protected readonly canSell = inject(AuthService).hasRole('admin', 'vendedor');
  protected readonly statuses = entries(INVOICE_STATUS_LABELS);

  readonly overdue = input<string>();
  readonly customerId = input<string>();

  protected readonly query = new ListQuery<InvoiceFilters>({
    sort: '-createdAt',
    filters: { status: null, customerId: null, from: null, to: null, overdue: null },
  });
  protected readonly invoices = httpResource<Paged<InvoiceListItem>>(() => ({ url: '/api/invoices', params: this.query.params() }));

  ngOnInit(): void {
    if (this.overdue() === 'true') this.setOverdue();
    if (this.customerId()) this.query.setFilter('customerId', +this.customerId()!);
  }

  setStatus(status: InvoiceStatus | null): void {
    this.query.filters.update((f) => ({ ...f, status, overdue: null }));
    this.query.page.set(1);
  }

  setOverdue(): void {
    this.query.filters.update((f) => ({ ...f, status: null, overdue: true }));
    this.query.page.set(1);
  }

  exportCsv(): void {
    this.api.download('/api/invoices/export', 'facturas.csv', this.query.params().delete('page').delete('pageSize')).subscribe();
  }
}
