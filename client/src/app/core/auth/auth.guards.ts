import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Role } from '../models';
import { AuthService } from './auth.service';

/** Only authenticated users; otherwise to /login keeping the requested URL. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? true : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** Login page only for guests. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? inject(Router).createUrlTree(['/']) : true;
};

/**
 * Role check driven by route data: `{ data: { roles: ['admin', 'deposito'] } }`.
 * It only hides screens — the API enforces the same rules on every request.
 */
export const roleGuard: CanActivateFn = (route) => {
  const roles = (route.data['roles'] as Role[] | undefined) ?? [];
  const auth = inject(AuthService);
  return roles.length === 0 || auth.hasRole(...roles) ? true : inject(Router).createUrlTree(['/dashboard']);
};
