import { DatePipe } from '@angular/common';
import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth/auth.service';
import { applyServerErrors } from '../../core/form-errors';
import { MOVEMENT_TYPE_LABELS, VAT_LABELS, entries } from '../../core/labels';
import { Category, Paged, ProductDetail, StockMovement, Supplier, VatRate } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { ArsPipe } from '../../shared/ui/ars.pipe';
import { FieldErrorComponent } from '../../shared/ui/field-error.component';

@Component({
  selector: 'app-product-form-page',
  imports: [ReactiveFormsModule, RouterLink, ArsPipe, DatePipe, FieldErrorComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <div>
        <a routerLink="/productos" class="back">← Productos</a>
        <h1>{{ isNew() ? 'Nuevo producto' : product()?.name }}</h1>
        @if (product(); as p) { <p class="muted"><code>{{ p.sku }}</code> · actualizado {{ p.updatedAt | date: 'short' }}</p> }
      </div>
    </header>

    <div class="layout-2">
      <form class="card form-grid" [formGroup]="form" (ngSubmit)="save()" novalidate>
        <label class="field">
          <span>SKU</span>
          <input formControlName="sku" placeholder="NB-LEN-14" [readonly]="!isNew()" />
          <app-field-error [control]="form.controls.sku" />
        </label>
        <label class="field span-2">
          <span>Nombre</span>
          <input formControlName="name" />
          <app-field-error [control]="form.controls.name" />
        </label>
        <label class="field">
          <span>Categoría</span>
          <select formControlName="categoryId">
            <option [ngValue]="null" disabled>Elegí una categoría</option>
            @for (c of categories.value() ?? []; track c.id) { <option [ngValue]="c.id">{{ c.name }}</option> }
          </select>
          <app-field-error [control]="form.controls.categoryId" />
        </label>
        <label class="field">
          <span>Proveedor</span>
          <select formControlName="supplierId">
            <option [ngValue]="null">Sin proveedor</option>
            @for (s of suppliers.value()?.items ?? []; track s.id) { <option [ngValue]="s.id">{{ s.name }}</option> }
          </select>
        </label>
        <label class="field">
          <span>Alícuota IVA</span>
          <select formControlName="vatRate">
            @for (v of vatOptions; track v.value) { <option [value]="v.value">{{ v.label }}</option> }
          </select>
        </label>
        <label class="field">
          <span>Precio neto (sin IVA)</span>
          <input type="number" min="0" step="0.01" formControlName="price" />
          <app-field-error [control]="form.controls.price" />
        </label>
        <label class="field">
          <span>Costo</span>
          <input type="number" min="0" step="0.01" formControlName="cost" />
          <app-field-error [control]="form.controls.cost" />
        </label>
        <label class="field">
          <span>Stock mínimo</span>
          <input type="number" min="0" step="1" formControlName="minimumStock" />
          <app-field-error [control]="form.controls.minimumStock" />
        </label>
        <label class="field span-3">
          <span>Descripción</span>
          <textarea rows="3" formControlName="description"></textarea>
        </label>
        <div class="span-3 form-footer">
          <div class="muted">
            Margen: <strong>{{ margin() }}%</strong> · Precio final con IVA: <strong>{{ finalPrice() | ars }}</strong>
          </div>
          @if (!isNew()) {
            <label class="check"><input type="checkbox" formControlName="isActive" /> Activo</label>
          }
          @if (canEdit) {
            <button class="btn btn-primary" type="submit" [disabled]="saving()">{{ saving() ? 'Guardando…' : 'Guardar' }}</button>
          }
        </div>
      </form>

      @if (product(); as p) {
        <aside class="stack">
          <section class="card">
            <h2 class="card-title">Stock por depósito</h2>
            <table class="table compact">
              <tbody>
                @for (s of p.stock; track s.warehouseId) {
                  <tr><td>{{ s.warehouseName }} <span class="muted">({{ s.warehouseCode }})</span></td><td class="num">{{ s.quantity }}</td></tr>
                }
                <tr class="total"><td>Total</td><td class="num" [class.danger]="p.isLowStock">{{ p.totalStock }}</td></tr>
              </tbody>
            </table>
            @if (p.isLowStock) { <p class="alert alert-warning">Por debajo del mínimo ({{ p.minimumStock }} u.).</p> }
          </section>
          <section class="card">
            <h2 class="card-title">Últimos movimientos</h2>
            <ul class="timeline">
              @for (m of movements.value()?.items ?? []; track m.id) {
                <li>
                  <span class="qty" [class.in]="m.quantity > 0" [class.out]="m.quantity < 0">{{ m.quantity > 0 ? '+' : '' }}{{ m.quantity }}</span>
                  <div>
                    <strong>{{ movementLabels[m.type] }}</strong> · {{ m.warehouseCode }}
                    <div class="muted small">{{ m.createdAt | date: 'short' }} · {{ m.reference ?? m.reason ?? '' }}</div>
                  </div>
                </li>
              } @empty { <li class="muted">Sin movimientos.</li> }
            </ul>
            <a routerLink="/stock" [queryParams]="{ productId: p.id }">Ver historial completo →</a>
          </section>
        </aside>
      }
    </div>
  `,
})
export class ProductFormPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  protected readonly canEdit = inject(AuthService).hasRole('admin', 'deposito');
  protected readonly vatOptions = entries(VAT_LABELS);
  protected readonly movementLabels = MOVEMENT_TYPE_LABELS;

  /** Route param (`/productos/:id`); undefined on `/productos/nuevo`. */
  readonly id = input<string>();

  protected readonly isNew = computed(() => !this.id());
  protected readonly product = signal<ProductDetail | null>(null);
  protected readonly saving = signal(false);

  protected readonly categories = httpResource<Category[]>(() => '/api/categories');
  protected readonly suppliers = httpResource<Paged<Supplier>>(() => ({ url: '/api/suppliers', params: { pageSize: 100 } }));
  protected readonly movements = httpResource<Paged<StockMovement>>(() =>
    this.id() ? { url: '/api/stock/movements', params: { productId: this.id()!, pageSize: 6 } } : undefined,
  );

  protected readonly form = inject(FormBuilder).group({
    sku: ['', [Validators.required, Validators.maxLength(32), Validators.pattern(/^[A-Za-z0-9._-]+$/)]],
    name: ['', [Validators.required, Validators.maxLength(150)]],
    description: [''],
    categoryId: [null as number | null, Validators.required],
    supplierId: [null as number | null],
    vatRate: ['General' as VatRate, Validators.required],
    price: [0, [Validators.required, Validators.min(0)]],
    cost: [0, [Validators.required, Validators.min(0)]],
    minimumStock: [0, [Validators.required, Validators.min(0)]],
    isActive: [true],
  });

  private readonly values = signal(this.form.getRawValue());
  protected readonly margin = computed(() => {
    const { price, cost } = this.values();
    return price && cost ? Math.round(((Number(price) - Number(cost)) / Number(price)) * 1000) / 10 : 0;
  });
  protected readonly finalPrice = computed(() => {
    const { price, vatRate } = this.values();
    return Number(price ?? 0) * (1 + ({ General: 0.21, Reducido: 0.105, Exento: 0 } as const)[vatRate ?? 'General']);
  });

  constructor() {
    this.form.valueChanges.subscribe(() => this.values.set(this.form.getRawValue()));
  }

  ngOnInit(): void {
    if (!this.canEdit) {
      this.form.disable();
    }
    const id = this.id();
    if (id) {
      this.api.getProduct(+id).subscribe((p) => {
        this.product.set(p);
        this.form.patchValue(p);
      });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const value = this.form.getRawValue();
    const request = this.isNew() ? this.api.createProduct(value) : this.api.updateProduct(+this.id()!, value);
    request.subscribe({
      next: (p) => {
        this.saving.set(false);
        this.product.set(p);
        this.toast.success(this.isNew() ? 'Producto creado.' : 'Cambios guardados.');
        if (this.isNew()) void this.router.navigate(['/productos', p.id]);
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        applyServerErrors(this.form, err).forEach((m) => this.toast.error(m));
      },
    });
  }
}
