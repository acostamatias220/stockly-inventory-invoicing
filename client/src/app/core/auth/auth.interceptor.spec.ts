import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    localStorage.setItem(
      'stockly.session',
      JSON.stringify({
        token: 'abc',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        user: { id: 1, email: 'admin@stockly.dev', fullName: 'Admin', role: 'admin', isActive: true, lastLoginAt: null },
      }),
    );
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), provideRouter([{ path: 'login', children: [] }])],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => {
    controller.verify();
    localStorage.clear();
  });

  it('adds the bearer token to API calls only', () => {
    http.get('/api/products').subscribe();
    http.get('https://example.com/other').subscribe();
    expect(controller.expectOne('/api/products').request.headers.get('Authorization')).toBe('Bearer abc');
    expect(controller.expectOne('https://example.com/other').request.headers.has('Authorization')).toBe(false);
  });

  it('logs out when the API answers 401', () => {
    http.get('/api/products').subscribe({ error: () => undefined });
    controller.expectOne('/api/products').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('keeps the session on 403 (authenticated but not allowed)', () => {
    http.get('/api/users').subscribe({ error: () => undefined });
    controller.expectOne('/api/users').flush(null, { status: 403, statusText: 'Forbidden' });
    expect(auth.isAuthenticated()).toBe(true);
  });
});
