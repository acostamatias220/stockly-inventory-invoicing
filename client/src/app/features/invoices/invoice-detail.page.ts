import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth/auth.service';
import { PAYMENT_METHOD_LABELS, TAX_CONDITION_LABELS, VAT_LABELS, entries } from '../../core/labels';
import { InvoiceDetail, PaymentMethod } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { ArsPipe } from '../../shared/ui/ars.pipe';
import { ModalComponent } from '../../shared/ui/modal.component';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';

@Component({
  selector: 'app-invoice-detail-page',
  imports: [RouterLink, DatePipe, FormsModule, ArsPipe, StatusBadgeComponent, ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './invoice-detail.page.scss',
  template: `
    @if (invoice(); as inv) {
      <header class="page-header">
        <div>
          <a routerLink="/facturas" class="back">← Facturas</a>
          <h1 class="mono">{{ inv.number ?? 'Borrador #' + inv.id }}</h1>
          <p><app-status-badge [status]="inv.status" [overdue]="inv.isOverdue" /> <span class="muted">creada por {{ inv.createdBy }} el {{ inv.createdAt | date: 'short' }}</span></p>
        </div>
        <div class="actions">
          <button type="button" class="btn btn-ghost" (click)="downloadPdf(inv)">⇩ PDF</button>
          @if (canSell && inv.status === 'Draft') {
            <button type="button" class="btn btn-ghost danger" (click)="remove(inv)">Eliminar</button>
            <a class="btn btn-ghost" [routerLink]="['editar']">Editar</a>
            <button type="button" class="btn btn-primary" [disabled]="busy()" (click)="run(api.issueInvoice(inv.id, inv.version), 'Factura emitida: stock descontado.')">Emitir</button>
          }
          @if (canSell && inv.status === 'Issued') {
            <button type="button" class="btn btn-primary" (click)="payOpen.set(true)">Registrar pago</button>
          }
          @if (canCancel()) {
            <button type="button" class="btn btn-ghost danger" (click)="cancelOpen.set(true)">Anular</button>
          }
        </div>
      </header>

      <article class="card document">
        <div class="doc-header">
          <div>
            <h2>Stockly Distribuidora S.R.L.</h2>
            <p class="muted">Depósito de salida: {{ inv.warehouseName }}</p>
          </div>
          <div class="letter">{{ inv.letter }}</div>
          <div class="doc-dates">
            <div><span class="muted">Emisión</span> {{ inv.issueDate ? (inv.issueDate | date: 'dd/MM/yyyy' : 'UTC') : '—' }}</div>
            <div><span class="muted">Vencimiento</span> <span [class.danger]="inv.isOverdue">{{ inv.dueDate | date: 'dd/MM/yyyy' : 'UTC' }}</span></div>
            <div><span class="muted">Punto de venta</span> {{ inv.pointOfSale.toString().padStart(4, '0') }}</div>
          </div>
        </div>

        <div class="doc-customer">
          <div><span class="muted">Cliente</span><strong>{{ inv.customer.name }}</strong></div>
          <div><span class="muted">CUIT / DNI</span><code>{{ inv.customer.taxId }}</code></div>
          <div><span class="muted">Condición IVA</span>{{ taxLabels[inv.customer.taxCondition] }}</div>
          <div><span class="muted">Domicilio</span>{{ inv.customer.address ?? '—' }}</div>
        </div>

        <div class="table-wrap">
          <table class="table">
            <thead><tr><th>SKU</th><th>Descripción</th><th class="num">Cant.</th><th class="num">P. unit.</th><th class="num">Bonif.</th><th>IVA</th><th class="num">Neto</th><th class="num">Total</th></tr></thead>
            <tbody>
              @for (l of inv.lines; track l.id) {
                <tr>
                  <td><code>{{ l.sku }}</code></td>
                  <td><a [routerLink]="['/productos', l.productId]">{{ l.description }}</a></td>
                  <td class="num">{{ l.quantity }}</td>
                  <td class="num">{{ l.unitPrice | ars }}</td>
                  <td class="num">{{ l.discountPercent ? l.discountPercent + '%' : '—' }}</td>
                  <td>{{ vatLabels[l.vatRate] }}</td>
                  <td class="num">{{ l.netAmount | ars }}</td>
                  <td class="num">{{ l.total | ars }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <div class="doc-footer">
          <div class="notes">
            @if (inv.notes) { <p><strong>Observaciones:</strong> {{ inv.notes }}</p> }
            @if (inv.status === 'Paid') { <p class="alert alert-success">Pagada el {{ inv.paidAt | date: 'short' }} · {{ paymentLabels[inv.paymentMethod!] }}</p> }
            @if (inv.status === 'Cancelled') { <p class="alert alert-error">Anulada el {{ inv.cancelledAt | date: 'short' }}: {{ inv.cancellationReason }}</p> }
            @if (inv.status === 'Draft') { <p class="alert alert-warning">Borrador: todavía no tiene número ni descontó stock.</p> }
          </div>
          <dl class="totals">
            <div><dt>Subtotal</dt><dd>{{ inv.subtotal | ars }}</dd></div>
            @if (inv.discountTotal) { <div><dt>Bonificaciones @if (inv.discountPercent) { ({{ inv.discountPercent }}% general) }</dt><dd>-{{ inv.discountTotal | ars }}</dd></div> }
            <div><dt>Neto gravado</dt><dd>{{ inv.netTotal | ars }}</dd></div>
            @if (inv.vat21) { <div><dt>IVA 21%</dt><dd>{{ inv.vat21 | ars }}</dd></div> }
            @if (inv.vat105) { <div><dt>IVA 10,5%</dt><dd>{{ inv.vat105 | ars }}</dd></div> }
            <div class="grand"><dt>Total</dt><dd>{{ inv.total | ars }}</dd></div>
          </dl>
        </div>
      </article>

      <app-modal title="Registrar pago" [open]="payOpen()" (closed)="payOpen.set(false)">
        <p>Total a cobrar: <strong>{{ inv.total | ars }}</strong></p>
        <div class="segmented">
          @for (m of paymentMethods; track m.value) {
            <button type="button" [class.active]="paymentMethod() === m.value" (click)="paymentMethod.set(m.value)">{{ m.label }}</button>
          }
        </div>
        <div class="form-footer">
          <button type="button" class="btn btn-ghost" (click)="payOpen.set(false)">Cancelar</button>
          <button type="button" class="btn btn-primary" [disabled]="busy()" (click)="run(api.payInvoice(inv.id, paymentMethod()), 'Pago registrado.')">Confirmar pago</button>
        </div>
      </app-modal>

      <app-modal title="Anular factura" [open]="cancelOpen()" (closed)="cancelOpen.set(false)">
        @if (inv.status === 'Issued') { <p class="alert alert-warning">La mercadería de esta factura vuelve al stock de {{ inv.warehouseName }}.</p> }
        <label class="field">
          <span>Motivo</span>
          <input [(ngModel)]="cancelReason" placeholder="Ej.: error en los datos del cliente" />
        </label>
        <div class="form-footer">
          <button type="button" class="btn btn-ghost" (click)="cancelOpen.set(false)">Volver</button>
          <button type="button" class="btn btn-danger" [disabled]="busy() || !cancelReason.trim()" (click)="run(api.cancelInvoice(inv.id, cancelReason), 'Factura anulada.')">Anular</button>
        </div>
      </app-modal>
    } @else {
      <div class="card skeleton-doc"></div>
    }
  `,
})
export class InvoiceDetailPage implements OnInit {
  protected readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  protected readonly canSell = this.auth.hasRole('admin', 'vendedor');
  protected readonly taxLabels = TAX_CONDITION_LABELS;
  protected readonly vatLabels = VAT_LABELS;
  protected readonly paymentLabels = PAYMENT_METHOD_LABELS;
  protected readonly paymentMethods = entries(PAYMENT_METHOD_LABELS);

  readonly id = input.required<string>();

  protected readonly invoice = signal<InvoiceDetail | null>(null);
  protected readonly busy = signal(false);
  protected readonly payOpen = signal(false);
  protected readonly cancelOpen = signal(false);
  protected readonly paymentMethod = signal<PaymentMethod>('Transferencia');
  protected cancelReason = '';

  /** Drafts: any seller. Issued: only admins (it moves stock back). Mirrors the API rule. */
  protected readonly canCancel = computed(() => {
    const status = this.invoice()?.status;
    return (status === 'Draft' && this.canSell) || (status === 'Issued' && this.auth.hasRole('admin'));
  });

  ngOnInit(): void {
    this.api.getInvoice(+this.id()).subscribe((inv) => this.invoice.set(inv));
  }

  run(action: Observable<InvoiceDetail>, message: string): void {
    this.busy.set(true);
    action.subscribe({
      next: (inv) => {
        this.invoice.set(inv);
        this.busy.set(false);
        this.payOpen.set(false);
        this.cancelOpen.set(false);
        this.toast.success(message);
      },
      error: () => {
        this.busy.set(false);
        // Stale data (409) or business rule (422): reload so the screen shows the real state.
        this.ngOnInit();
      },
    });
  }

  remove(inv: InvoiceDetail): void {
    if (!confirm('¿Eliminar este borrador?')) return;
    this.api.deleteInvoice(inv.id).subscribe(() => {
      this.toast.success('Borrador eliminado.');
      void this.router.navigate(['/facturas']);
    });
  }

  downloadPdf(inv: InvoiceDetail): void {
    this.api.download(`/api/invoices/${inv.id}/pdf`, `factura-${inv.number ?? inv.id}.pdf`).subscribe();
  }
}
