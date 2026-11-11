import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { fileNameFrom, saveBlob } from './http-params';
import {
  Category,
  Customer,
  InvoiceDetail,
  Paged,
  ProductListItem,
  PaymentMethod,
  ProductDetail,
  SaveInvoiceRequest,
  StockMovement,
  Supplier,
  User,
  Warehouse,
} from './models';

/** Typed wrapper over the REST API for commands and downloads. Lists are read with `httpResource`. */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  // Products ---------------------------------------------------------------------------------
  searchProducts(params: Record<string, string | number | boolean>): Observable<Paged<ProductListItem>> {
    return this.http.get<Paged<ProductListItem>>('/api/products', { params });
  }

  getProduct(id: number): Observable<ProductDetail> {
    return this.http.get<ProductDetail>(`/api/products/${id}`);
  }

  createProduct(body: unknown): Observable<ProductDetail> {
    return this.http.post<ProductDetail>('/api/products', body);
  }

  updateProduct(id: number, body: unknown): Observable<ProductDetail> {
    return this.http.put<ProductDetail>(`/api/products/${id}`, body);
  }

  // Catalog ----------------------------------------------------------------------------------
  saveCategory(body: unknown, id?: number): Observable<Category> {
    return id ? this.http.put<Category>(`/api/categories/${id}`, body) : this.http.post<Category>('/api/categories', body);
  }

  deleteCategory(id: number): Observable<void> {
    return this.http.delete<void>(`/api/categories/${id}`);
  }

  saveWarehouse(body: unknown, id?: number): Observable<Warehouse> {
    return id ? this.http.put<Warehouse>(`/api/warehouses/${id}`, body) : this.http.post<Warehouse>('/api/warehouses', body);
  }

  // Stock ------------------------------------------------------------------------------------
  registerMovement(body: unknown): Observable<StockMovement[]> {
    return this.http.post<StockMovement[]>('/api/stock/movements', body);
  }

  transfer(body: unknown): Observable<StockMovement[]> {
    return this.http.post<StockMovement[]>('/api/stock/transfers', body);
  }

  // Parties ----------------------------------------------------------------------------------
  saveCustomer(body: unknown, id?: number): Observable<Customer> {
    return id ? this.http.put<Customer>(`/api/customers/${id}`, body) : this.http.post<Customer>('/api/customers', body);
  }

  saveSupplier(body: unknown, id?: number): Observable<Supplier> {
    return id ? this.http.put<Supplier>(`/api/suppliers/${id}`, body) : this.http.post<Supplier>('/api/suppliers', body);
  }

  // Invoices ---------------------------------------------------------------------------------
  getInvoice(id: number): Observable<InvoiceDetail> {
    return this.http.get<InvoiceDetail>(`/api/invoices/${id}`);
  }

  saveInvoice(body: SaveInvoiceRequest, id?: number): Observable<InvoiceDetail> {
    return id ? this.http.put<InvoiceDetail>(`/api/invoices/${id}`, body) : this.http.post<InvoiceDetail>('/api/invoices', body);
  }

  deleteInvoice(id: number): Observable<void> {
    return this.http.delete<void>(`/api/invoices/${id}`);
  }

  issueInvoice(id: number, version: number): Observable<InvoiceDetail> {
    return this.http.post<InvoiceDetail>(`/api/invoices/${id}/issue`, null, { params: { version } });
  }

  payInvoice(id: number, paymentMethod: PaymentMethod): Observable<InvoiceDetail> {
    return this.http.post<InvoiceDetail>(`/api/invoices/${id}/pay`, { paymentMethod });
  }

  cancelInvoice(id: number, reason: string): Observable<InvoiceDetail> {
    return this.http.post<InvoiceDetail>(`/api/invoices/${id}/cancel`, { reason });
  }

  // Users ------------------------------------------------------------------------------------
  saveUser(body: unknown, id?: number): Observable<User> {
    return id ? this.http.put<User>(`/api/users/${id}`, body) : this.http.post<User>('/api/users', body);
  }

  // Files ------------------------------------------------------------------------------------
  /** Downloads a CSV/PDF honoring the server's file name. */
  download(url: string, fallbackName: string, params?: HttpParams): Observable<void> {
    return this.http.get(url, { params, responseType: 'blob', observe: 'response' }).pipe(
      map((response: HttpResponse<Blob>) => {
        saveBlob(response.body!, fileNameFrom(response.headers.get('Content-Disposition'), fallbackName));
      }),
    );
  }
}
