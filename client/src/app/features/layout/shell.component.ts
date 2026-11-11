import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ROLE_LABELS } from '../../core/labels';
import { Role } from '../../core/models';

interface NavItem {
  path: string;
  label: string;
  icon: string;
  roles?: Role[];
}

const NAV: NavItem[] = [
  { path: '/dashboard', label: 'Dashboard', icon: 'M3 13h8V3H3zm0 8h8v-6H3zm10 0h8V11h-8zm0-18v6h8V3z' },
  { path: '/facturas', label: 'Facturas', icon: 'M6 2h9l5 5v15H6zm8 1.5V8h4.5M9 12h8M9 16h8M9 8h3' },
  { path: '/productos', label: 'Productos', icon: 'M21 8l-9-5-9 5v8l9 5 9-5zM3 8l9 5 9-5M12 13v8' },
  { path: '/stock', label: 'Movimientos', icon: 'M7 7h13l-4-4M17 17H4l4 4' },
  { path: '/clientes', label: 'Clientes', icon: 'M16 11a4 4 0 1 0-8 0 4 4 0 0 0 8 0zM4 21a8 8 0 0 1 16 0' },
  { path: '/proveedores', label: 'Proveedores', icon: 'M3 7h11v10H3zM14 10h4l3 3v4h-7M7 20a2 2 0 1 0 0-4 2 2 0 0 0 0 4zm10 0a2 2 0 1 0 0-4 2 2 0 0 0 0 4z' },
  { path: '/configuracion', label: 'Configuración', icon: 'M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-2.9 1.2V21a2 2 0 1 1-4 0v-.1A1.7 1.7 0 0 0 7 19.4l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1A1.7 1.7 0 0 0 3 13.7H3a2 2 0 1 1 0-4h.1A1.7 1.7 0 0 0 4.6 7l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1A1.7 1.7 0 0 0 10.3 3V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 2.9 1.2l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0 1.2 2.9H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z', roles: ['admin', 'deposito'] },
  { path: '/usuarios', label: 'Usuarios', icon: 'M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zm14 10v-2a4 4 0 0 0-3-3.9M16 3.1a4 4 0 0 1 0 7.8', roles: ['admin'] },
];

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './shell.component.scss',
  template: `
    <div class="shell" [class.nav-open]="navOpen()">
      <aside class="sidebar">
        <a class="brand" routerLink="/dashboard">
          <span class="logo" aria-hidden="true">S</span>
          <span>Stockly</span>
        </a>
        <nav aria-label="Principal">
          @for (item of nav(); track item.path) {
            <a [routerLink]="item.path" routerLinkActive="active" (click)="navOpen.set(false)">
              <svg viewBox="0 0 24 24" aria-hidden="true"><path [attr.d]="item.icon" /></svg>
              {{ item.label }}
            </a>
          }
        </nav>
        <div class="sidebar-footer muted">v1.0 · ARS</div>
      </aside>

      <div class="main">
        <header class="topbar">
          <button type="button" class="icon-btn menu-btn" aria-label="Abrir menú" (click)="navOpen.set(!navOpen())">☰</button>
          <div class="spacer"></div>
          @if (auth.user(); as user) {
            <div class="user">
              <span class="avatar" aria-hidden="true">{{ initials() }}</span>
              <div class="user-info">
                <strong>{{ user.fullName }}</strong>
                <span class="muted">{{ roleLabel() }}</span>
              </div>
              <button type="button" class="btn btn-ghost btn-sm" (click)="auth.logout()">Salir</button>
            </div>
          }
        </header>
        <main class="content">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  protected readonly navOpen = signal(false);

  protected readonly nav = computed(() => NAV.filter((item) => !item.roles || (this.auth.role() && item.roles.includes(this.auth.role()!))));
  protected readonly roleLabel = computed(() => (this.auth.role() ? ROLE_LABELS[this.auth.role()!] : ''));
  protected readonly initials = computed(() =>
    (this.auth.user()?.fullName ?? '')
      .split(' ')
      .map((p) => p[0])
      .slice(0, 2)
      .join('')
      .toUpperCase(),
  );
}
