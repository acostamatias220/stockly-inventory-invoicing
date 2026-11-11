import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { LoginPage } from './login.page';

describe('LoginPage', () => {
  let login: (email: string, password: string) => Observable<unknown>;

  async function render(returnUrl?: string) {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: { login: (e: string, p: string) => login(e, p) } }],
    });
    const fixture = TestBed.createComponent(LoginPage);
    if (returnUrl) fixture.componentRef.setInput('returnUrl', returnUrl);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const type = (selector: string, value: string) => {
      const input = el.querySelector<HTMLInputElement>(selector)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    const submit = async () => {
      el.querySelector('form')!.dispatchEvent(new Event('submit'));
      await fixture.whenStable();
    };
    return { el, type, submit };
  }

  it('shows validation errors and does not call the API when the form is empty', async () => {
    login = vi.fn();
    const { el, submit } = await render();
    await submit();
    expect(login).not.toHaveBeenCalled();
    expect(el.querySelectorAll('.field-error').length).toBe(2);
  });

  it('logs in and navigates to the return url', async () => {
    login = vi.fn(() => of({}));
    const { type, submit } = await render('/facturas/nueva');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    type('input[type=email]', 'admin@stockly.dev');
    type('input[type=password]', 'secreto-123');
    await submit();
    expect(login).toHaveBeenCalledWith('admin@stockly.dev', 'secreto-123');
    expect(navigate).toHaveBeenCalledWith('/facturas/nueva');
  });

  it('shows a friendly message on wrong credentials', async () => {
    login = () => throwError(() => new HttpErrorResponse({ status: 401 }));
    const { el, type, submit } = await render();
    type('input[type=email]', 'admin@stockly.dev');
    type('input[type=password]', 'mal');
    await submit();
    expect(el.querySelector('.alert-error')?.textContent).toContain('Email o contraseña incorrectos');
  });
});
