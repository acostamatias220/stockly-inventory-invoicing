import { Routes } from '@angular/router';
import { roleGuard } from '../../core/auth/auth.guards';

export const PRODUCT_ROUTES: Routes = [
  { path: '', title: 'Productos · Stockly', loadComponent: () => import('./product-list.page').then((m) => m.ProductListPage) },
  {
    path: 'nuevo',
    title: 'Nuevo producto · Stockly',
    canActivate: [roleGuard],
    data: { roles: ['admin', 'deposito'] },
    loadComponent: () => import('./product-form.page').then((m) => m.ProductFormPage),
  },
  { path: ':id', title: 'Producto · Stockly', loadComponent: () => import('./product-form.page').then((m) => m.ProductFormPage) },
];
