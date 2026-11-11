import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { LoginResponse, Role, User } from '../models';

interface StoredSession {
  token: string;
  expiresAt: string;
  user: User;
}

const STORAGE_KEY = 'stockly.session';

/**
 * Session state kept in signals. The access token lives in localStorage so a refresh keeps the user
 * logged in; it is short-lived (8 h) and cleared on expiry or on any 401 from the API.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly session = signal<StoredSession | null>(this.restore());
  private expiryTimer?: ReturnType<typeof setTimeout>;

  readonly user = computed(() => this.session()?.user ?? null);
  readonly token = computed(() => this.session()?.token ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly role = computed<Role | null>(() => this.session()?.user.role ?? null);

  constructor() {
    this.scheduleExpiry();
  }

  login(email: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('/api/auth/login', { email, password }).pipe(
      tap((response) => {
        const session: StoredSession = { token: response.accessToken, expiresAt: response.expiresAt, user: response.user };
        localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
        this.session.set(session);
        this.scheduleExpiry();
      }),
    );
  }

  logout(redirect = true): void {
    localStorage.removeItem(STORAGE_KEY);
    this.session.set(null);
    clearTimeout(this.expiryTimer);
    if (redirect) {
      void this.router.navigate(['/login']);
    }
  }

  /** True when the current user has any of the given roles. */
  hasRole(...roles: Role[]): boolean {
    const role = this.role();
    return role !== null && roles.includes(role);
  }

  private restore(): StoredSession | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      const session = raw ? (JSON.parse(raw) as StoredSession) : null;
      if (session && new Date(session.expiresAt).getTime() > Date.now()) {
        return session;
      }
    } catch {
      /* corrupted storage → start clean */
    }
    localStorage.removeItem(STORAGE_KEY);
    return null;
  }

  private scheduleExpiry(): void {
    clearTimeout(this.expiryTimer);
    const session = this.session();
    if (!session) {
      return;
    }
    const ms = new Date(session.expiresAt).getTime() - Date.now();
    this.expiryTimer = setTimeout(() => this.logout(), Math.max(0, Math.min(ms, 2_147_000_000)));
  }
}
