import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/auth/auth.guards';

/** Every feature is lazy loaded: the initial bundle only contains the shell and the login. */
export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Ingresar · Stockly',
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./features/layout/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard · Stockly',
        loadComponent: () => import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage),
      },
      { path: 'productos', loadChildren: () => import('./features/products/products.routes').then((m) => m.PRODUCT_ROUTES) },
      {
        path: 'stock',
        title: 'Movimientos de stock · Stockly',
        loadComponent: () => import('./features/stock/stock-movements.page').then((m) => m.StockMovementsPage),
      },
      { path: 'facturas', loadChildren: () => import('./features/invoices/invoices.routes').then((m) => m.INVOICE_ROUTES) },
      {
        path: 'clientes',
        title: 'Clientes · Stockly',
        loadComponent: () => import('./features/parties/customers.page').then((m) => m.CustomersPage),
      },
      {
        path: 'proveedores',
        title: 'Proveedores · Stockly',
        loadComponent: () => import('./features/parties/suppliers.page').then((m) => m.SuppliersPage),
      },
      {
        path: 'configuracion',
        title: 'Configuración · Stockly',
        canActivate: [roleGuard],
        data: { roles: ['admin', 'deposito'] },
        loadComponent: () => import('./features/settings/settings.page').then((m) => m.SettingsPage),
      },
      {
        path: 'usuarios',
        title: 'Usuarios · Stockly',
        canActivate: [roleGuard],
        data: { roles: ['admin'] },
        loadComponent: () => import('./features/users/users.page').then((m) => m.UsersPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
