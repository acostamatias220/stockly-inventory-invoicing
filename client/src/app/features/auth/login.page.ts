import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { problemMessage } from '../../core/error.interceptor';
import { FieldErrorComponent } from '../../shared/ui/field-error.component';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, FieldErrorComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './login.page.scss',
  template: `
    <main class="login">
      <section class="login-card">
        <div class="brand">
          <span class="logo" aria-hidden="true">S</span>
          <div>
            <h1>Stockly</h1>
            <p class="muted">Inventario y facturación</p>
          </div>
        </div>

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <label class="field">
            <span>Email</span>
            <input type="email" formControlName="email" autocomplete="username" placeholder="admin@stockly.dev" />
            <app-field-error [control]="form.controls.email" />
          </label>
          <label class="field">
            <span>Contraseña</span>
            <input type="password" formControlName="password" autocomplete="current-password" />
            <app-field-error [control]="form.controls.password" />
          </label>

          @if (error()) {
            <p class="alert alert-error" role="alert">{{ error() }}</p>
          }

          <button class="btn btn-primary btn-block" type="submit" [disabled]="loading()">
            {{ loading() ? 'Ingresando…' : 'Ingresar' }}
          </button>
        </form>

        <p class="demo-hint muted">
          Usuarios demo: <code>admin@</code>, <code>ventas@</code> y <code>deposito&#64;stockly.dev</code>
          (la contraseña se define en <code>Seed:DemoPassword</code>).
        </p>
      </section>
    </main>
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Bound from the query string by the router (withComponentInputBinding). */
  readonly returnUrl = input<string>();

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => void this.router.navigateByUrl(this.returnUrl() || '/dashboard'),
      error: (err: HttpErrorResponse) => {
        this.error.set(err.status === 401 ? 'Email o contraseña incorrectos.' : problemMessage(err));
        this.loading.set(false);
      },
    });
  }
}
