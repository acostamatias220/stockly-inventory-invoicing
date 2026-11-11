import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth/auth.service';
import { applyServerErrors, isValidCuit } from '../../core/form-errors';
import { TAX_CONDITION_LABELS, entries } from '../../core/labels';
import { Customer, Paged, TaxCondition } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { ArsPipe } from '../../shared/ui/ars.pipe';
import { FieldErrorComponent } from '../../shared/ui/field-error.component';
import { ListQuery } from '../../shared/ui/list-query';
import { ModalComponent } from '../../shared/ui/modal.component';
import { PaginationComponent } from '../../shared/ui/pagination.component';
import { SortButtonComponent } from '../../shared/ui/sort-button.component';

/** CUIT required (with check digit) unless the customer is Consumidor Final, who may use a DNI. */
function taxIdValidator(group: AbstractControl): ValidationErrors | null {
  const taxId = group.get('taxId');
  const condition = group.get('taxCondition')?.value as TaxCondition;
  const value = String(taxId?.value ?? '');
  const ok = condition === 'ConsumidorFinal' ? /^\d{7,8}$/.test(value) || isValidCuit(value) : isValidCuit(value);
  if (!ok && value) {
    taxId?.setErrors({ ...(taxId.errors ?? {}), cuit: true });
  } else if (taxId?.hasError('cuit')) {
    const rest = { ...taxId.errors };
    delete rest['cuit'];
    taxId.setErrors(Object.keys(rest).length ? rest : null);
  }
  return null;
}

@Component({
  selector: 'app-customers-page',
  imports: [ReactiveFormsModule, RouterLink, ArsPipe, PaginationComponent, SortButtonComponent, ModalComponent, FieldErrorComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <div>
        <h1>Clientes</h1>
        <p class="muted">La condición frente al IVA define si se emite Factura A o B</p>
      </div>
      @if (canEdit) { <button type="button" class="btn btn-primary" (click)="edit(null)">+ Nuevo cliente</button> }
    </header>

    <section class="card">
      <div class="toolbar">
        <input type="search" class="search" placeholder="Razón social, CUIT/DNI o email…" aria-label="Buscar clientes"
          [value]="query.search()" (input)="query.setSearch($any($event.target).value)" />
      </div>
      <div class="table-wrap" [class.loading]="customers.isLoading()">
        <table class="table">
          <thead>
            <tr>
              <th><app-sort-button key="name" label="razón social" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Razón social</app-sort-button></th>
              <th><app-sort-button key="taxId" label="CUIT" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">CUIT / DNI</app-sort-button></th>
              <th>Condición IVA</th>
              <th>Contacto</th>
              <th class="num">Saldo a cobrar</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (c of customers.value()?.items ?? []; track c.id) {
              <tr [class.inactive]="!c.isActive">
                <td><strong>{{ c.name }}</strong><div class="muted small">{{ c.address }}</div></td>
                <td><code>{{ c.taxId }}</code></td>
                <td><span class="badge badge-letter">{{ c.invoiceLetter }}</span> {{ taxLabels[c.taxCondition] }}</td>
                <td>{{ c.email }}<div class="muted small">{{ c.phone }}</div></td>
                <td class="num" [class.warning]="c.balance > 0">{{ c.balance | ars }}</td>
                <td class="row-actions">
                  <a class="btn btn-ghost btn-sm" routerLink="/facturas" [queryParams]="{ customerId: c.id }">Facturas</a>
                  @if (canEdit) { <button type="button" class="btn btn-ghost btn-sm" (click)="edit(c)">Editar</button> }
                </td>
              </tr>
            } @empty { <tr><td colspan="6" class="empty">No hay clientes.</td></tr> }
          </tbody>
        </table>
      </div>
      @if (customers.value(); as page) {
        <app-pagination [page]="page.page" [totalPages]="page.totalPages" [totalCount]="page.totalCount" (pageChange)="query.page.set($event)" />
      }
    </section>

    <app-modal [title]="editingId() ? 'Editar cliente' : 'Nuevo cliente'" [open]="open()" (closed)="open.set(false)">
      <form class="form-grid cols-2" [formGroup]="form" (ngSubmit)="save()" novalidate>
        <label class="field span-2"><span>Razón social / nombre</span><input formControlName="name" /><app-field-error [control]="form.controls.name" /></label>
        <label class="field">
          <span>Condición IVA</span>
          <select formControlName="taxCondition">
            @for (t of taxOptions; track t.value) { <option [value]="t.value">{{ t.label }}</option> }
          </select>
        </label>
        <label class="field"><span>CUIT / DNI</span><input formControlName="taxId" placeholder="30-71234567-1" /><app-field-error [control]="form.controls.taxId" /></label>
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
export class CustomersPage {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  protected readonly canEdit = inject(AuthService).hasRole('admin', 'vendedor');
  protected readonly taxLabels = TAX_CONDITION_LABELS;
  protected readonly taxOptions = entries(TAX_CONDITION_LABELS);

  protected readonly query = new ListQuery({ sort: 'name' });
  protected readonly customers = httpResource<Paged<Customer>>(() => ({ url: '/api/customers', params: this.query.params() }));

  protected readonly open = signal(false);
  protected readonly saving = signal(false);
  protected readonly editingId = signal<number | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group(
    {
      name: ['', [Validators.required, Validators.maxLength(150)]],
      taxCondition: ['ResponsableInscripto' as TaxCondition],
      taxId: ['', Validators.required],
      email: ['', Validators.email],
      phone: [''],
      address: [''],
      isActive: [true],
    },
    { validators: taxIdValidator },
  );

  edit(customer: Customer | null): void {
    this.editingId.set(customer?.id ?? null);
    this.form.reset();
    if (customer) {
      this.form.patchValue({ ...customer, email: customer.email ?? '', phone: customer.phone ?? '', address: customer.address ?? '' });
    }
    this.open.set(true);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.api.saveCustomer(this.form.getRawValue(), this.editingId() ?? undefined).subscribe({
      next: () => {
        this.saving.set(false);
        this.open.set(false);
        this.toast.success('Cliente guardado.');
        this.customers.reload();
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        applyServerErrors(this.form, err).forEach((m) => this.toast.error(m));
      },
    });
  }
}
