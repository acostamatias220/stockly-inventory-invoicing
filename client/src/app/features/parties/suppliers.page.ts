import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth/auth.service';
import { applyServerErrors, isValidCuit } from '../../core/form-errors';
import { Paged, Supplier } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { FieldErrorComponent } from '../../shared/ui/field-error.component';
import { ListQuery } from '../../shared/ui/list-query';
import { ModalComponent } from '../../shared/ui/modal.component';
import { PaginationComponent } from '../../shared/ui/pagination.component';
import { SortButtonComponent } from '../../shared/ui/sort-button.component';

const cuitValidator = (control: AbstractControl): ValidationErrors | null =>
  !control.value || isValidCuit(String(control.value)) ? null : { cuit: true };

@Component({
  selector: 'app-suppliers-page',
  imports: [ReactiveFormsModule, PaginationComponent, SortButtonComponent, ModalComponent, FieldErrorComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <div>
        <h1>Proveedores</h1>
        <p class="muted">Se asocian a productos y a las entradas de mercadería</p>
      </div>
      @if (canEdit) { <button type="button" class="btn btn-primary" (click)="edit(null)">+ Nuevo proveedor</button> }
    </header>

    <section class="card">
      <div class="toolbar">
        <input type="search" class="search" placeholder="Razón social, CUIT o contacto…" aria-label="Buscar proveedores"
          [value]="query.search()" (input)="query.setSearch($any($event.target).value)" />
      </div>
      <div class="table-wrap" [class.loading]="suppliers.isLoading()">
        <table class="table">
          <thead>
            <tr>
              <th><app-sort-button key="name" label="razón social" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Razón social</app-sort-button></th>
              <th><app-sort-button key="taxId" label="CUIT" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">CUIT</app-sort-button></th>
              <th>Contacto</th>
              <th class="num">Productos</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (s of suppliers.value()?.items ?? []; track s.id) {
              <tr [class.inactive]="!s.isActive">
                <td><strong>{{ s.name }}</strong><div class="muted small">{{ s.address }}</div></td>
                <td><code>{{ s.taxId }}</code></td>
                <td>{{ s.contactName }}<div class="muted small">{{ s.email }} · {{ s.phone }}</div></td>
                <td class="num">{{ s.productCount }}</td>
                <td class="row-actions">
                  @if (canEdit) { <button type="button" class="btn btn-ghost btn-sm" (click)="edit(s)">Editar</button> }
                </td>
              </tr>
            } @empty { <tr><td colspan="5" class="empty">No hay proveedores.</td></tr> }
          </tbody>
        </table>
      </div>
      @if (suppliers.value(); as page) {
        <app-pagination [page]="page.page" [totalPages]="page.totalPages" [totalCount]="page.totalCount" (pageChange)="query.page.set($event)" />
      }
    </section>

    <app-modal [title]="editingId() ? 'Editar proveedor' : 'Nuevo proveedor'" [open]="open()" (closed)="open.set(false)">
      <form class="form-grid cols-2" [formGroup]="form" (ngSubmit)="save()" novalidate>
        <label class="field span-2"><span>Razón social</span><input formControlName="name" /><app-field-error [control]="form.controls.name" /></label>
        <label class="field"><span>CUIT</span><input formControlName="taxId" placeholder="30-71234567-1" /><app-field-error [control]="form.controls.taxId" /></label>
        <label class="field"><span>Contacto</span><input formControlName="contactName" /></label>
        <label class="field"><span>Email</span><input type="email" formControlName="email" /><app-field-error [control]="form.controls.email" /></label>
        <label class="field"><span>Teléfono</span><input formControlName="phone" /></label>
        <label class="field span-2"><span>Domicilio</span><input formControlName="address" /></label>
        <div class="span-2 form-footer">
          @if (editingId()) { <label class="check"><input type="checkbox" formControlName="isActive" /> Activo</label> }
          <button type="button" class="btn btn-ghost" (click)="open.set(false)">Cancelar</button>
          <button type="submit" class="btn btn-primary" [disabled]="saving()">Guardar</button>
        </div>
      </form>
    </app-modal>
  `,
})
export class SuppliersPage {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  protected readonly canEdit = inject(AuthService).hasRole('admin', 'deposito');

  protected readonly query = new ListQuery({ sort: 'name' });
  protected readonly suppliers = httpResource<Paged<Supplier>>(() => ({ url: '/api/suppliers', params: this.query.params() }));

  protected readonly open = signal(false);
  protected readonly saving = signal(false);
  protected readonly editingId = signal<number | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    taxId: ['', [Validators.required, cuitValidator]],
    contactName: [''],
    email: ['', Validators.email],
    phone: [''],
    address: [''],
    isActive: [true],
  });

  edit(supplier: Supplier | null): void {
    this.editingId.set(supplier?.id ?? null);
    this.form.reset();
    if (supplier) {
      this.form.patchValue({
        ...supplier,
        contactName: supplier.contactName ?? '',
        email: supplier.email ?? '',
        phone: supplier.phone ?? '',
        address: supplier.address ?? '',
      });
    }
    this.open.set(true);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.api.saveSupplier(this.form.getRawValue(), this.editingId() ?? undefined).subscribe({
      next: () => {
        this.saving.set(false);
        this.open.set(false);
        this.toast.success('Proveedor guardado.');
        this.suppliers.reload();
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        applyServerErrors(this.form, err).forEach((m) => this.toast.error(m));
      },
    });
  }
}
