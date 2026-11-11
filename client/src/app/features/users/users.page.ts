import { DatePipe } from '@angular/common';
import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth/auth.service';
import { applyServerErrors } from '../../core/form-errors';
import { ROLE_LABELS } from '../../core/labels';
import { Role, User, UserRoleName } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { FieldErrorComponent } from '../../shared/ui/field-error.component';
import { ModalComponent } from '../../shared/ui/modal.component';

const ROLE_NAMES: Record<Role, UserRoleName> = { admin: 'Admin', vendedor: 'Vendedor', deposito: 'Deposito' };

@Component({
  selector: 'app-users-page',
  imports: [ReactiveFormsModule, DatePipe, ModalComponent, FieldErrorComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <div>
        <h1>Usuarios</h1>
        <p class="muted">Roles: <strong>admin</strong> (todo), <strong>vendedor</strong> (clientes y facturas), <strong>depósito</strong> (productos y stock)</p>
      </div>
      <button type="button" class="btn btn-primary" (click)="edit(null)">+ Nuevo usuario</button>
    </header>

    <section class="card">
      <table class="table">
        <thead><tr><th>Nombre</th><th>Email</th><th>Rol</th><th>Último ingreso</th><th>Estado</th><th></th></tr></thead>
        <tbody>
          @for (u of users.value() ?? []; track u.id) {
            <tr [class.inactive]="!u.isActive">
              <td><strong>{{ u.fullName }}</strong></td>
              <td>{{ u.email }}</td>
              <td><span class="badge" [class]="'badge badge-role-' + u.role">{{ roleLabels[u.role] }}</span></td>
              <td>{{ u.lastLoginAt ? (u.lastLoginAt | date: 'short') : 'Nunca' }}</td>
              <td>{{ u.isActive ? 'Activo' : 'Inactivo' }}</td>
              <td class="row-actions"><button type="button" class="btn btn-ghost btn-sm" (click)="edit(u)">Editar</button></td>
            </tr>
          }
        </tbody>
      </table>
    </section>

    <app-modal [title]="editing() ? 'Editar usuario' : 'Nuevo usuario'" [open]="open()" (closed)="open.set(false)">
      <form class="form-grid cols-2" [formGroup]="form" (ngSubmit)="save()" novalidate>
        <label class="field span-2"><span>Nombre completo</span><input formControlName="fullName" /><app-field-error [control]="form.controls.fullName" /></label>
        <label class="field"><span>Email</span><input type="email" formControlName="email" [readonly]="!!editing()" /><app-field-error [control]="form.controls.email" /></label>
        <label class="field">
          <span>Rol</span>
          <select formControlName="role">
            <option value="Admin">Administrador</option>
            <option value="Vendedor">Vendedor</option>
            <option value="Deposito">Depósito</option>
          </select>
        </label>
        <label class="field span-2">
          <span>{{ editing() ? 'Nueva contraseña (opcional)' : 'Contraseña' }}</span>
          <input type="password" formControlName="password" autocomplete="new-password" placeholder="Mínimo 10 caracteres, letras y números" />
          <app-field-error [control]="form.controls.password" />
        </label>
        <div class="span-2 form-footer">
          @if (editing()) { <label class="check"><input type="checkbox" formControlName="isActive" /> Activo</label> }
          <button type="button" class="btn btn-ghost" (click)="open.set(false)">Cancelar</button>
          <button type="submit" class="btn btn-primary" [disabled]="saving()">Guardar</button>
        </div>
      </form>
    </app-modal>
  `,
})
export class UsersPage {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  protected readonly me = inject(AuthService).user;
  protected readonly roleLabels = ROLE_LABELS;
  protected readonly users = httpResource<User[]>(() => '/api/users');
  protected readonly open = signal(false);
  protected readonly saving = signal(false);
  protected readonly editing = signal<User | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    fullName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    role: ['Vendedor' as UserRoleName],
    password: [''],
    isActive: [true],
  });

  edit(user: User | null): void {
    this.editing.set(user);
    this.form.reset();
    if (user) {
      this.form.patchValue({ fullName: user.fullName, email: user.email, role: ROLE_NAMES[user.role], isActive: user.isActive });
    }
    this.open.set(true);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const user = this.editing();
    const body = user
      ? { fullName: v.fullName, role: v.role, isActive: v.isActive, newPassword: v.password || null }
      : { fullName: v.fullName, email: v.email, role: v.role, password: v.password };
    this.saving.set(true);
    this.api.saveUser(body, user?.id).subscribe({
      next: () => {
        this.saving.set(false);
        this.open.set(false);
        this.toast.success('Usuario guardado.');
        this.users.reload();
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        const unmapped = applyServerErrors(this.form, err);
        if (err.status === 400 && this.form.controls.password.valid) {
          const problem = err.error as { errors?: Record<string, string[]> };
          const pwd = problem.errors?.['password'] ?? problem.errors?.['newPassword'];
          if (pwd) this.form.controls.password.setErrors({ server: pwd[0] });
        }
        unmapped.filter((m) => !m.includes('contraseña')).forEach((m) => this.toast.error(m));
      },
    });
  }
}
