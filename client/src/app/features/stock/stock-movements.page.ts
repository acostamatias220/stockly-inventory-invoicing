import { DatePipe } from '@angular/common';
import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth/auth.service';
import { applyServerErrors } from '../../core/form-errors';
import { MOVEMENT_TYPE_LABELS, entries } from '../../core/labels';
import { Paged, ProductListItem, StockMovement, StockMovementType, Supplier, Warehouse } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { FieldErrorComponent } from '../../shared/ui/field-error.component';
import { ListQuery } from '../../shared/ui/list-query';
import { ModalComponent } from '../../shared/ui/modal.component';
import { PaginationComponent } from '../../shared/ui/pagination.component';
import { SortButtonComponent } from '../../shared/ui/sort-button.component';

interface MovementFilters extends Record<string, string | number | boolean | null> {
  productId: number | null;
  warehouseId: number | null;
  type: StockMovementType | null;
  from: string | null;
  to: string | null;
}

const MANUAL_TYPES = ['Entrada', 'Salida', 'Ajuste'] as const;
type ManualType = (typeof MANUAL_TYPES)[number];

@Component({
  selector: 'app-stock-movements-page',
  imports: [DatePipe, RouterLink, ReactiveFormsModule, PaginationComponent, SortButtonComponent, ModalComponent, FieldErrorComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <div>
        <h1>Movimientos de stock</h1>
        <p class="muted">Auditoría completa: cada cambio de stock queda registrado con usuario, motivo y saldo</p>
      </div>
      <div class="actions">
        <button type="button" class="btn btn-ghost" (click)="exportCsv()">⇩ Exportar CSV</button>
        @if (canEdit) {
          <button type="button" class="btn btn-ghost" (click)="transferOpen.set(true)">⇄ Transferir</button>
          <button type="button" class="btn btn-primary" (click)="movementOpen.set(true)">+ Registrar movimiento</button>
        }
      </div>
    </header>

    <section class="card">
      <div class="toolbar">
        <input type="search" class="search" placeholder="Producto, SKU o referencia…" aria-label="Buscar movimientos"
          [value]="query.search()" (input)="query.setSearch($any($event.target).value)" />
        <select aria-label="Depósito" (change)="query.setFilter('warehouseId', $any($event.target).value || null)">
          <option value="">Todos los depósitos</option>
          @for (w of warehouses.value() ?? []; track w.id) { <option [value]="w.id">{{ w.code }} · {{ w.name }}</option> }
        </select>
        <select aria-label="Tipo" (change)="query.setFilter('type', $any($event.target).value || null)">
          <option value="">Todos los tipos</option>
          @for (t of types; track t.value) { <option [value]="t.value">{{ t.label }}</option> }
        </select>
        <label class="inline-field">Desde <input type="date" (change)="query.setFilter('from', $any($event.target).value || null)" /></label>
        <label class="inline-field">Hasta <input type="date" (change)="query.setFilter('to', $any($event.target).value || null)" /></label>
        @if (query.filters().productId) {
          <button type="button" class="chip removable" (click)="query.setFilter('productId', null)">Producto #{{ query.filters().productId }} ✕</button>
        }
      </div>

      <div class="table-wrap" [class.loading]="movements.isLoading()">
        <table class="table">
          <thead>
            <tr>
              <th><app-sort-button key="createdAt" label="fecha" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Fecha</app-sort-button></th>
              <th><app-sort-button key="type" label="tipo" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Tipo</app-sort-button></th>
              <th><app-sort-button key="product" label="producto" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Producto</app-sort-button></th>
              <th><app-sort-button key="warehouse" label="depósito" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Depósito</app-sort-button></th>
              <th class="num"><app-sort-button key="quantity" label="cantidad" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Cantidad</app-sort-button></th>
              <th class="num">Saldo</th>
              <th>Motivo / referencia</th>
              <th>Usuario</th>
            </tr>
          </thead>
          <tbody>
            @for (m of movements.value()?.items ?? []; track m.id) {
              <tr>
                <td class="nowrap">{{ m.createdAt | date: 'dd/MM/yy HH:mm' }}</td>
                <td><span class="badge" [class]="'badge badge-mov-' + m.type.toLowerCase()">{{ labels[m.type] }}</span></td>
                <td><a [routerLink]="['/productos', m.productId]">{{ m.productName }}</a><span class="cell-sub mono muted">{{ m.sku }}</span></td>
                <td>{{ m.warehouseCode }}</td>
                <td class="num" [class.success]="m.quantity > 0" [class.danger]="m.quantity < 0">{{ m.quantity > 0 ? '+' : '' }}{{ m.quantity }}</td>
                <td class="num">{{ m.balanceAfter }}</td>
                <td>
                  {{ m.reason }}
                  @if (m.reference) {
                    @if (m.invoiceId) { <a [routerLink]="['/facturas', m.invoiceId]">{{ m.reference }}</a> }
                    @else { <span class="muted">{{ m.reference }}</span> }
                  }
                  @if (m.supplierName) { <span class="muted"> · {{ m.supplierName }}</span> }
                </td>
                <td class="nowrap">{{ m.userName }}</td>
              </tr>
            } @empty {
              <tr><td colspan="8" class="empty">Sin movimientos para los filtros elegidos.</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (movements.value(); as page) {
        <app-pagination [page]="page.page" [totalPages]="page.totalPages" [totalCount]="page.totalCount" (pageChange)="query.page.set($event)" />
      }
    </section>

    <app-modal title="Registrar movimiento" [open]="movementOpen()" (closed)="movementOpen.set(false)">
      <form class="form-grid cols-2" [formGroup]="movementForm" (ngSubmit)="saveMovement()" novalidate>
        <div class="field span-2" role="group" aria-label="Tipo de movimiento">
          <span>Tipo</span>
          <div class="segmented">
            @for (t of manualTypes; track t.value) {
              <button type="button" [class.active]="movementForm.controls.type.value === t.value" (click)="movementForm.controls.type.setValue(t.value)">{{ t.label }}</button>
            }
          </div>
        </div>
        <label class="field span-2">
          <span>Producto</span>
          <select formControlName="productId">
            <option [ngValue]="null" disabled>Elegí un producto</option>
            @for (p of products.value()?.items ?? []; track p.id) { <option [ngValue]="p.id">{{ p.sku }} · {{ p.name }}</option> }
          </select>
          <app-field-error [control]="movementForm.controls.productId" />
        </label>
        <label class="field">
          <span>Depósito</span>
          <select formControlName="warehouseId">
            @for (w of warehouses.value() ?? []; track w.id) { <option [ngValue]="w.id">{{ w.code }} · {{ w.name }}</option> }
          </select>
        </label>
        <label class="field">
          <span>{{ movementForm.controls.type.value === 'Ajuste' ? 'Cantidad contada' : 'Cantidad' }}</span>
          <input type="number" min="0" step="1" formControlName="quantity" />
          <app-field-error [control]="movementForm.controls.quantity" />
        </label>
        @if (movementForm.controls.type.value === 'Entrada') {
          <label class="field">
            <span>Proveedor</span>
            <select formControlName="supplierId">
              <option [ngValue]="null">—</option>
              @for (s of suppliers.value()?.items ?? []; track s.id) { <option [ngValue]="s.id">{{ s.name }}</option> }
            </select>
          </label>
          <label class="field">
            <span>Remito / referencia</span>
            <input formControlName="reference" placeholder="R-0001-00001234" />
          </label>
        }
        <label class="field span-2">
          <span>Motivo</span>
          <input formControlName="reason" [placeholder]="movementForm.controls.type.value === 'Ajuste' ? 'Conteo físico mensual' : 'Opcional en entradas'" />
          <app-field-error [control]="movementForm.controls.reason" />
        </label>
        <div class="span-2 form-footer">
          <button type="button" class="btn btn-ghost" (click)="movementOpen.set(false)">Cancelar</button>
          <button type="submit" class="btn btn-primary" [disabled]="saving()">Registrar</button>
        </div>
      </form>
    </app-modal>

    <app-modal title="Transferir entre depósitos" [open]="transferOpen()" (closed)="transferOpen.set(false)">
      <form class="form-grid cols-2" [formGroup]="transferForm" (ngSubmit)="saveTransfer()" novalidate>
        <label class="field span-2">
          <span>Producto</span>
          <select formControlName="productId">
            <option [ngValue]="null" disabled>Elegí un producto</option>
            @for (p of products.value()?.items ?? []; track p.id) { <option [ngValue]="p.id">{{ p.sku }} · {{ p.name }}</option> }
          </select>
        </label>
        <label class="field">
          <span>Origen</span>
          <select formControlName="fromWarehouseId">
            @for (w of warehouses.value() ?? []; track w.id) { <option [ngValue]="w.id">{{ w.code }} · {{ w.name }}</option> }
          </select>
        </label>
        <label class="field">
          <span>Destino</span>
          <select formControlName="toWarehouseId">
            @for (w of warehouses.value() ?? []; track w.id) { <option [ngValue]="w.id">{{ w.code }} · {{ w.name }}</option> }
          </select>
          <app-field-error [control]="transferForm.controls.toWarehouseId" />
        </label>
        <label class="field">
          <span>Cantidad</span>
          <input type="number" min="1" step="1" formControlName="quantity" />
        </label>
        <label class="field">
          <span>Motivo</span>
          <input formControlName="reason" placeholder="Reposición sucursal" />
        </label>
        <div class="span-2 form-footer">
          <button type="button" class="btn btn-ghost" (click)="transferOpen.set(false)">Cancelar</button>
          <button type="submit" class="btn btn-primary" [disabled]="saving()">Transferir</button>
        </div>
      </form>
    </app-modal>
  `,
})
export class StockMovementsPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);
  protected readonly canEdit = inject(AuthService).hasRole('admin', 'deposito');
  protected readonly labels = MOVEMENT_TYPE_LABELS;
  protected readonly types = entries(MOVEMENT_TYPE_LABELS);
  protected readonly manualTypes = this.types.filter((t): t is { value: ManualType; label: string } =>
    (MANUAL_TYPES as readonly string[]).includes(t.value),
  );

  /** `/stock?productId=5` (from the product page). */
  readonly productId = input<string>();

  protected readonly movementOpen = signal(false);
  protected readonly transferOpen = signal(false);
  protected readonly saving = signal(false);

  protected readonly query = new ListQuery<MovementFilters>({
    sort: '-createdAt',
    filters: { productId: null, warehouseId: null, type: null, from: null, to: null },
  });
  protected readonly movements = httpResource<Paged<StockMovement>>(() => ({ url: '/api/stock/movements', params: this.query.params() }));
  protected readonly warehouses = httpResource<Warehouse[]>(() => '/api/warehouses');
  protected readonly products = httpResource<Paged<ProductListItem>>(() =>
    this.canEdit ? { url: '/api/products', params: { pageSize: 100, sort: 'name' } } : undefined,
  );
  protected readonly suppliers = httpResource<Paged<Supplier>>(() =>
    this.canEdit ? { url: '/api/suppliers', params: { pageSize: 100 } } : undefined,
  );

  protected readonly movementForm = this.fb.group({
    type: ['Entrada' as ManualType],
    productId: [null as number | null, Validators.required],
    warehouseId: [1, Validators.required],
    quantity: [1, [Validators.required, Validators.min(0)]],
    reason: [''],
    reference: [''],
    supplierId: [null as number | null],
  });

  protected readonly transferForm = this.fb.group({
    productId: [null as number | null, Validators.required],
    fromWarehouseId: [1, Validators.required],
    toWarehouseId: [2, Validators.required],
    quantity: [1, [Validators.required, Validators.min(1)]],
    reason: [''],
  });

  ngOnInit(): void {
    const productId = this.productId();
    if (productId) {
      this.query.setFilter('productId', +productId);
    }
  }

  saveMovement(): void {
    const value = this.movementForm.getRawValue();
    const body = { ...value, supplierId: value.type === 'Entrada' ? value.supplierId : null };
    this.submit(this.api.registerMovement(body), this.movementForm, () => this.movementOpen.set(false), 'Movimiento registrado.');
  }

  saveTransfer(): void {
    this.submit(this.api.transfer(this.transferForm.getRawValue()), this.transferForm, () => this.transferOpen.set(false), 'Transferencia realizada.');
  }

  exportCsv(): void {
    this.api.download('/api/stock/movements/export', 'movimientos.csv', this.query.params().delete('page').delete('pageSize')).subscribe();
  }

  private submit(
    request: ReturnType<ApiService['transfer']>,
    form: typeof this.movementForm | typeof this.transferForm,
    close: () => void,
    message: string,
  ): void {
    if (form.invalid) {
      form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    request.subscribe({
      next: () => {
        this.saving.set(false);
        close();
        this.toast.success(message);
        this.movements.reload();
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        applyServerErrors(form, err).forEach((m) => this.toast.error(m));
      },
    });
  }
}
