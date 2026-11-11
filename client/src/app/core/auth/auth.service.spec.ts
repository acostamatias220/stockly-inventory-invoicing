import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LoginResponse } from '../models';
import { AuthService } from './auth.service';

const response = (expiresInMs: number): LoginResponse => ({
  accessToken: 'header.payload.signature',
  expiresAt: new Date(Date.now() + expiresInMs).toISOString(),
  user: { id: 1, email: 'ventas@stockly.dev', fullName: 'Lucía Fernández', role: 'vendedor', isActive: true, lastLoginAt: null },
});

describe('AuthService', () => {
  let http: HttpTestingController;

  function setup(): AuthService {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(AuthService);
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => http.verify());

  it('stores the session on login and exposes role helpers', () => {
    const auth = setup();
    auth.login('ventas@stockly.dev', 'whatever').subscribe();
    const req = http.expectOne('/api/auth/login');
    expect(req.request.method).toBe('POST');
    req.flush(response(60_000));

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.token()).toBe('header.payload.signature');
    expect(auth.hasRole('admin', 'vendedor')).toBe(true);
    expect(auth.hasRole('deposito')).toBe(false);
    expect(localStorage.getItem('stockly.session')).toContain('ventas@stockly.dev');
  });

  it('restores a valid session from storage and ignores an expired one', () => {
    localStorage.setItem('stockly.session', JSON.stringify({ token: 't', expiresAt: response(60_000).expiresAt, user: response(0).user }));
    expect(setup().isAuthenticated()).toBe(true);

    TestBed.resetTestingModule();
    localStorage.setItem('stockly.session', JSON.stringify({ token: 't', expiresAt: response(-1_000).expiresAt, user: response(0).user }));
    expect(setup().isAuthenticated()).toBe(false);
    expect(localStorage.getItem('stockly.session')).toBeNull();
  });

  it('survives corrupted storage', () => {
    localStorage.setItem('stockly.session', '{not json');
    expect(setup().user()).toBeNull();
  });

  it('logout clears everything', () => {
    const auth = setup();
    auth.login('a@b.c', 'x').subscribe();
    http.expectOne('/api/auth/login').flush(response(60_000));
    auth.logout(false);
    expect(auth.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('stockly.session')).toBeNull();
  });
});
