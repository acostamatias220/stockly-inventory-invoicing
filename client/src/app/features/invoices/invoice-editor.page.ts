import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { map, startWith, switchMap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { applyServerErrors } from '../../core/form-errors';
import { TAX_CONDITION_LABELS, VAT_LABELS } from '../../core/labels';
import { Customer, InvoiceDetail, Paged, SaveInvoiceRequest, VatRate, Warehouse } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { ArsPipe } from '../../shared/ui/ars.pipe';
import { FieldErrorComponent } from '../../shared/ui/field-error.component';
import { calculateTotals } from './invoice-math';

type LineForm = FormGroup<{
  productId: FormControl<number | null>;
  quantity: FormControl<number>;
  unitPrice: FormControl<number>;
  discountPercent: FormControl<number>;
  vatRate: FormControl<VatRate>;
}>;

@Component({
  selector: 'app-invoice-editor-page',
  imports: [ReactiveFormsModule, RouterLink, ArsPipe, FieldErrorComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './invoice-editor.page.scss',
  template: `
    <header class="page-header">
      <div>
        <a routerLink="/facturas" class="back">← Facturas</a>
        <h1>{{ id() ? 'Editar borrador #' + id() : 'Nueva factura' }}</h1>
        <p class="muted">El stock se descuenta recién al emitir. Los precios se toman del producto y se pueden ajustar.</p>
      </div>
      @if (letter(); as l) { <span class="letter-box" [attr.aria-label]="'Factura ' + l">{{ l }}</span> }
    </header>

    <form [formGroup]="form" (ngSubmit)="save(false)" novalidate class="stack">
      <section class="card form-grid">
        <label class="field span-2">
          <span>Cliente</span>
          <select formControlName="customerId">
            <option [ngValue]="null" disabled>Elegí un cliente</option>
            @for (c of customers.value()?.items ?? []; track c.id) {
              <option [ngValue]="c.id">{{ c.name }} · {{ taxLabels[c.taxCondition] }}</option>
            }
          </select>
          <app-field-error [control]="form.controls.customerId" />
        </label>
        <label class="field">
          <span>Depósito de salida</span>
          <select formControlName="warehouseId">
            @for (w of warehouses.value() ?? []; track w.id) { <option [ngValue]="w.id">{{ w.code }} · {{ w.name }}</option> }
          </select>
        </label>
        <label class="field">
          <span>Vencimiento</span>
          <input type="date" formControlName="dueDate" />
        </label>
        <label class="field">
          <span>Descuento general (%)</span>
          <input type="number" min="0" max="100" step="0.5" formControlName="discountPercent" />
          <app-field-error [control]="form.controls.discountPercent" />
        </label>
        <label class="field span-3">
          <span>Observaciones</span>
          <input formControlName="notes" placeholder="Ej.: entrega en depósito del cliente" />
        </label>
      </section>

      <section class="card">
        <h2 class="card-title">Ítems</h2>
        <div class="table-wrap">
          <table class="table lines" formArrayName="lines">
            <thead>
              <tr>
                <th class="col-product">Producto</th>
                <th class="num">Disp.</th>
                <th class="num">Cant.</th>
                <th class="num">P. unitario</th>
                <th class="num">Bonif. %</th>
                <th>IVA</th>
                <th class="num">Neto</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (line of lines.controls; track line; let i = $index) {
                <tr [formGroupName]="i">
                  <td>
                    <select formControlName="productId" (change)="onProductChange(i)" [attr.aria-label]="'Producto del ítem ' + (i + 1)">
                      <option [ngValue]="null" disabled>Elegí un producto</option>
                      @for (p of products(); track p.id) { <option [ngValue]="p.id">{{ p.sku }} · {{ p.name }}</option> }
                    </select>
                    <app-field-error [control]="line.controls.productId" />
                  </td>
                  <td class="num" [class.danger]="available(line.controls.productId.value) < line.controls.quantity.value">
                    {{ line.controls.productId.value ? available(line.controls.productId.value) : '—' }}
                  </td>
                  <td class="num"><input type="number" min="1" step="1" formControlName="quantity" class="input-sm" aria-label="Cantidad" /></td>
                  <td class="num"><input type="number" min="0" step="0.01" formControlName="unitPrice" class="input-md" aria-label="Precio unitario" /></td>
                  <td class="num"><input type="number" min="0" max="100" step="0.5" formControlName="discountPercent" class="input-sm" aria-label="Bonificación" /></td>
                  <td>{{ vatLabels[line.controls.vatRate.value] }}</td>
                  <td class="num">{{ totals().lines[i]?.net | ars }}</td>
                  <td><button type="button" class="icon-btn" (click)="removeLine(i)" [attr.aria-label]="'Quitar ítem ' + (i + 1)">🗑</button></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <button type="button" class="btn btn-ghost" (click)="addLine()">+ Agregar ítem</button>
        @if (lines.length === 0) { <p class="muted">Agregá al menos un ítem para poder emitir.</p> }
      </section>

      <section class="summary">
        <dl class="card totals">
          <div><dt>Subtotal</dt><dd>{{ totals().subtotal | ars }}</dd></div>
          <div><dt>Bonificaciones</dt><dd>-{{ totals().discountTotal | ars }}</dd></div>
          <div><dt>Neto gravado</dt><dd>{{ totals().netTotal | ars }}</dd></div>
          <div><dt>IVA 21%</dt><dd>{{ totals().vat21 | ars }}</dd></div>
          <div><dt>IVA 10,5%</dt><dd>{{ totals().vat105 | ars }}</dd></div>
          <div class="grand"><dt>Total</dt><dd>{{ totals().total | ars }}</dd></div>
        </dl>
        <div class="summary-actions">
          <button type="submit" class="btn btn-ghost" [disabled]="saving()">Guardar borrador</button>
          <button type="button" class="btn btn-primary" [disabled]="saving() || lines.length === 0" (click)="save(true)">Guardar y emitir</button>
        </div>
      </section>
    </form>
  `,
})
export class InvoiceEditorPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder).nonNullable;
  protected readonly taxLabels = TAX_CONDITION_LABELS;
  protected readonly vatLabels = VAT_LABELS;

  /** Route param on `/facturas/:id/editar`. */
  readonly id = input<string>();

  protected readonly saving = signal(false);
  private version: number | undefined;

  protected readonly form = this.fb.group({
    customerId: this.fb.control<number | null>(null, Validators.required),
    warehouseId: this.fb.control(1, Validators.required),
    dueDate: this.fb.control<string | null>(null),
    discountPercent: this.fb.control(0, [Validators.min(0), Validators.max(100)]),
    notes: this.fb.control(''),
    lines: this.fb.array<LineForm>([]),
  });

  protected get lines(): FormArray<LineForm> {
    return this.form.controls.lines;
  }

  private readonly value = toSignal(this.form.valueChanges.pipe(startWith(null), map(() => this.form.getRawValue())), {
    requireSync: true,
  });

  protected readonly customers = httpResource<Paged<Customer>>(() => ({ url: '/api/customers', params: { pageSize: 100, active: true } }));
  protected readonly warehouses = httpResource<Warehouse[]>(() => '/api/warehouses');

  /** Products with stock of the selected warehouse (re-queried when the warehouse changes). */
  private readonly productsPage = toSignal(
    this.form.controls.warehouseId.valueChanges.pipe(
      startWith(this.form.controls.warehouseId.value),
      switchMap((warehouseId) =>
        this.api.searchProducts({ pageSize: 100, sort: 'name', active: true, warehouseId }),
      ),
    ),
  );
  protected readonly products = computed(() => this.productsPage()?.items ?? []);

  protected readonly letter = computed(() => {
    const customer = this.customers.value()?.items.find((c) => c.id === this.value().customerId);
    return customer?.invoiceLetter ?? null;
  });

  protected readonly totals = computed(() =>
    calculateTotals(
      this.value().lines.map((l) => ({ quantity: l.quantity, unitPrice: l.unitPrice, discountPercent: l.discountPercent, vatRate: l.vatRate })),
      this.value().discountPercent,
    ),
  );

  ngOnInit(): void {
    const id = this.id();
    if (!id) {
      this.addLine();
      return;
    }
    this.api.getInvoice(+id).subscribe((invoice) => this.load(invoice));
  }

  protected available(productId: number | null): number {
    return this.products().find((p) => p.id === productId)?.totalStock ?? 0;
  }

  addLine(): void {
    this.lines.push(
      this.fb.group({
        productId: this.fb.control<number | null>(null, Validators.required),
        quantity: this.fb.control(1, [Validators.required, Validators.min(1)]),
        unitPrice: this.fb.control(0, [Validators.required, Validators.min(0)]),
        discountPercent: this.fb.control(0, [Validators.min(0), Validators.max(100)]),
        vatRate: this.fb.control<VatRate>('General'),
      }),
    );
  }

  removeLine(index: number): void {
    this.lines.removeAt(index);
  }

  /** Copies the product's current price and VAT rate into the line. */
  onProductChange(index: number): void {
    const line = this.lines.at(index);
    const product = this.products().find((p) => p.id === line.controls.productId.value);
    if (product) {
      line.patchValue({ unitPrice: product.price, vatRate: product.vatRate });
    }
  }

  save(issue: boolean): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const request: SaveInvoiceRequest = {
      customerId: v.customerId!,
      warehouseId: v.warehouseId,
      dueDate: v.dueDate || null,
      discountPercent: v.discountPercent,
      notes: v.notes || null,
      lines: v.lines.map((l) => ({ productId: l.productId!, quantity: l.quantity, unitPrice: l.unitPrice, discountPercent: l.discountPercent })),
      version: this.version,
    };

    this.saving.set(true);
    const id = this.id() ? +this.id()! : undefined;
    this.api
      .saveInvoice(request, id)
      .pipe(switchMap((saved) => (issue ? this.api.issueInvoice(saved.id, saved.version) : [saved])))
      .subscribe({
        next: (invoice) => {
          this.toast.success(issue ? `Factura ${invoice.number} emitida.` : 'Borrador guardado.');
          void this.router.navigate(['/facturas', invoice.id]);
        },
        error: (err: HttpErrorResponse) => {
          this.saving.set(false);
          applyServerErrors(this.form, err).forEach((m) => this.toast.error(m));
        },
      });
  }

  private load(invoice: InvoiceDetail): void {
    if (invoice.status !== 'Draft') {
      void this.router.navigate(['/facturas', invoice.id]);
      return;
    }
    this.version = invoice.version;
    this.form.patchValue({
      customerId: invoice.customer.id,
      warehouseId: invoice.warehouseId,
      dueDate: invoice.dueDate,
      discountPercent: invoice.discountPercent,
      notes: invoice.notes ?? '',
    });
    this.lines.clear();
    for (const line of invoice.lines) {
      this.addLine();
      this.lines.at(-1).patchValue(line);
    }
  }
}
