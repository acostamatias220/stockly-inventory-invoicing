import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth/auth.service';
import { applyServerErrors } from '../../core/form-errors';
import { Category, Warehouse } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { FieldErrorComponent } from '../../shared/ui/field-error.component';
import { ModalComponent } from '../../shared/ui/modal.component';

@Component({
  selector: 'app-settings-page',
  imports: [ReactiveFormsModule, ModalComponent, FieldErrorComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <div>
        <h1>Configuración</h1>
        <p class="muted">Categorías de productos y depósitos</p>
      </div>
    </header>

    <div class="layout-2 equal">
      <section class="card">
        <header class="card-header-row">
          <h2 class="card-title">Categorías</h2>
          <button type="button" class="btn btn-ghost btn-sm" (click)="editCategory(null)">+ Nueva</button>
        </header>
        <table class="table">
          <thead><tr><th>Nombre</th><th class="num">Productos</th><th></th></tr></thead>
          <tbody>
            @for (c of categories.value() ?? []; track c.id) {
              <tr>
                <td><span class="chip" [style.--chip]="c.color">{{ c.name }}</span><div class="muted small">{{ c.description }}</div></td>
                <td class="num">{{ c.productCount }}</td>
                <td class="row-actions">
                  <button type="button" class="btn btn-ghost btn-sm" (click)="editCategory(c)">Editar</button>
                  <button type="button" class="btn btn-ghost btn-sm danger" [disabled]="c.productCount > 0" (click)="deleteCategory(c)"
                    [title]="c.productCount > 0 ? 'Tiene productos asociados' : 'Eliminar'">Eliminar</button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </section>

      <section class="card">
        <header class="card-header-row">
          <h2 class="card-title">Depósitos</h2>
          @if (isAdmin) { <button type="button" class="btn btn-ghost btn-sm" (click)="editWarehouse(null)">+ Nuevo</button> }
        </header>
        <table class="table">
          <thead><tr><th>Código</th><th>Nombre</th><th>Estado</th><th></th></tr></thead>
          <tbody>
            @for (w of warehouses.value() ?? []; track w.id) {
              <tr [class.inactive]="!w.isActive">
                <td><code>{{ w.code }}</code></td>
                <td>{{ w.name }}<div class="muted small">{{ w.address }}</div></td>
                <td>{{ w.isActive ? 'Activo' : 'Inactivo' }}</td>
                <td class="row-actions">@if (isAdmin) { <button type="button" class="btn btn-ghost btn-sm" (click)="editWarehouse(w)">Editar</button> }</td>
              </tr>
            }
          </tbody>
        </table>
      </section>
    </div>

    <app-modal [title]="categoryId() ? 'Editar categoría' : 'Nueva categoría'" [open]="categoryOpen()" (closed)="categoryOpen.set(false)">
      <form class="form-grid cols-2" [formGroup]="categoryForm" (ngSubmit)="saveCategory()" novalidate>
        <label class="field"><span>Nombre</span><input formControlName="name" /><app-field-error [control]="categoryForm.controls.name" /></label>
        <label class="field"><span>Color</span><input type="color" formControlName="color" /></label>
        <label class="field span-2"><span>Descripción</span><input formControlName="description" /></label>
        <div class="span-2 form-footer">
          <button type="button" class="btn btn-ghost" (click)="categoryOpen.set(false)">Cancelar</button>
          <button type="submit" class="btn btn-primary">Guardar</button>
        </div>
      </form>
    </app-modal>

    <app-modal [title]="warehouseId() ? 'Editar depósito' : 'Nuevo depósito'" [open]="warehouseOpen()" (closed)="warehouseOpen.set(false)">
      <form class="form-grid cols-2" [formGroup]="warehouseForm" (ngSubmit)="saveWarehouse()" novalidate>
        <label class="field"><span>Código</span><input formControlName="code" [readonly]="!!warehouseId()" placeholder="SUR" /><app-field-error [control]="warehouseForm.controls.code" /></label>
        <label class="field"><span>Nombre</span><input formControlName="name" /><app-field-error [control]="warehouseForm.controls.name" /></label>
        <label class="field span-2"><span>Dirección</span><input formControlName="address" /></label>
        <div class="span-2 form-footer">
          @if (warehouseId()) { <label class="check"><input type="checkbox" formControlName="isActive" /> Activo</label> }
          <button type="button" class="btn btn-ghost" (click)="warehouseOpen.set(false)">Cancelar</button>
          <button type="submit" class="btn btn-primary">Guardar</button>
        </div>
      </form>
    </app-modal>
  `,
})
export class SettingsPage {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder).nonNullable;
  protected readonly isAdmin = inject(AuthService).hasRole('admin');

  protected readonly categories = httpResource<Category[]>(() => '/api/categories');
  protected readonly warehouses = httpResource<Warehouse[]>(() => '/api/warehouses');

  protected readonly categoryOpen = signal(false);
  protected readonly categoryId = signal<number | null>(null);
  protected readonly categoryForm = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(80)]],
    description: [''],
    color: ['#6366F1'],
  });

  protected readonly warehouseOpen = signal(false);
  protected readonly warehouseId = signal<number | null>(null);
  protected readonly warehouseForm = this.fb.group({
    code: ['', [Validators.required, Validators.maxLength(10), Validators.pattern(/^[A-Za-z0-9-]+$/)]],
    name: ['', Validators.required],
    address: [''],
    isActive: [true],
  });

  editCategory(c: Category | null): void {
    this.categoryId.set(c?.id ?? null);
    this.categoryForm.reset();
    if (c) this.categoryForm.patchValue({ name: c.name, description: c.description ?? '', color: c.color });
    this.categoryOpen.set(true);
  }

  saveCategory(): void {
    if (this.categoryForm.invalid) return this.categoryForm.markAllAsTouched();
    const v = this.categoryForm.getRawValue();
    this.api.saveCategory({ ...v, color: v.color.toUpperCase() }, this.categoryId() ?? undefined).subscribe({
      next: () => this.done(() => this.categoryOpen.set(false), () => this.categories.reload()),
      error: (err: HttpErrorResponse) => applyServerErrors(this.categoryForm, err).forEach((m) => this.toast.error(m)),
    });
  }

  deleteCategory(c: Category): void {
    if (!confirm(`¿Eliminar la categoría "${c.name}"?`)) return;
    this.api.deleteCategory(c.id).subscribe(() => this.done(() => undefined, () => this.categories.reload()));
  }

  editWarehouse(w: Warehouse | null): void {
    this.warehouseId.set(w?.id ?? null);
    this.warehouseForm.reset();
    if (w) this.warehouseForm.patchValue({ ...w, address: w.address ?? '' });
    this.warehouseOpen.set(true);
  }

  saveWarehouse(): void {
    if (this.warehouseForm.invalid) return this.warehouseForm.markAllAsTouched();
    this.api.saveWarehouse(this.warehouseForm.getRawValue(), this.warehouseId() ?? undefined).subscribe({
      next: () => this.done(() => this.warehouseOpen.set(false), () => this.warehouses.reload()),
      error: (err: HttpErrorResponse) => applyServerErrors(this.warehouseForm, err).forEach((m) => this.toast.error(m)),
    });
  }

  private done(close: () => void, reload: () => void): void {
    close();
    reload();
    this.toast.success('Cambios guardados.');
  }
}
