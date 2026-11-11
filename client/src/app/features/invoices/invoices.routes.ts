import { Routes } from '@angular/router';
import { roleGuard } from '../../core/auth/auth.guards';

const sellers = { roles: ['admin', 'vendedor'] };

export const INVOICE_ROUTES: Routes = [
  { path: '', title: 'Facturas · Stockly', loadComponent: () => import('./invoice-list.page').then((m) => m.InvoiceListPage) },
  {
    path: 'nueva',
    title: 'Nueva factura · Stockly',
    canActivate: [roleGuard],
    data: sellers,
    loadComponent: () => import('./invoice-editor.page').then((m) => m.InvoiceEditorPage),
  },
  { path: ':id', title: 'Factura · Stockly', loadComponent: () => import('./invoice-detail.page').then((m) => m.InvoiceDetailPage) },
  {
    path: ':id/editar',
    title: 'Editar borrador · Stockly',
    canActivate: [roleGuard],
    data: sellers,
    loadComponent: () => import('./invoice-editor.page').then((m) => m.InvoiceEditorPage),
  },
];
