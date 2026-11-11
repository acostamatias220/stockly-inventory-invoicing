import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { Role } from '../models';
import { authGuard, roleGuard } from './auth.guards';
import { AuthService } from './auth.service';

describe('guards', () => {
  const role = signal<Role | null>(null);
  const fakeAuth = {
    isAuthenticated: () => role() !== null,
    hasRole: (...roles: Role[]) => role() !== null && roles.includes(role()!),
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: AuthService, useValue: fakeAuth }] });
  });

  const run = (guard: typeof roleGuard, data: Record<string, unknown> = {}, url = '/facturas') =>
    TestBed.runInInjectionContext(() =>
      guard({ data } as unknown as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
    );

  it('authGuard redirects guests to /login keeping the return url', () => {
    role.set(null);
    const result = run(authGuard) as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/login?returnUrl=%2Ffacturas');
  });

  it('authGuard lets authenticated users in', () => {
    role.set('vendedor');
    expect(run(authGuard)).toBe(true);
  });

  it('roleGuard allows matching roles and redirects the rest to the dashboard', () => {
    role.set('deposito');
    expect(run(roleGuard, { roles: ['admin', 'deposito'] })).toBe(true);
    const denied = run(roleGuard, { roles: ['admin'] }) as UrlTree;
    expect(TestBed.inject(Router).serializeUrl(denied)).toBe('/dashboard');
  });

  it('roleGuard without roles only needs a session', () => {
    role.set('vendedor');
    expect(run(roleGuard)).toBe(true);
  });
});
