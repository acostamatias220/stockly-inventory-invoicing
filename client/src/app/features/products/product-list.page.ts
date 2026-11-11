import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth/auth.service';
import { VAT_LABELS } from '../../core/labels';
import { Category, Paged, ProductListItem } from '../../core/models';
import { ArsPipe } from '../../shared/ui/ars.pipe';
import { ListQuery } from '../../shared/ui/list-query';
import { PaginationComponent } from '../../shared/ui/pagination.component';
import { SortButtonComponent } from '../../shared/ui/sort-button.component';

interface ProductFilters extends Record<string, string | number | boolean | null> {
  categoryId: number | null;
  lowStock: boolean | null;
  active: boolean | null;
}

@Component({
  selector: 'app-product-list-page',
  imports: [RouterLink, ArsPipe, PaginationComponent, SortButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <div>
        <h1>Productos</h1>
        <p class="muted">Catálogo, precios y stock total por producto</p>
      </div>
      <div class="actions">
        <button type="button" class="btn btn-ghost" (click)="exportCsv()">⇩ Exportar CSV</button>
        @if (canEdit) {
          <a class="btn btn-primary" routerLink="nuevo">+ Nuevo producto</a>
        }
      </div>
    </header>

    <section class="card">
      <div class="toolbar">
        <input type="search" class="search" placeholder="Buscar por nombre o SKU…" aria-label="Buscar productos"
          [value]="query.search()" (input)="query.setSearch($any($event.target).value)" />
        <select aria-label="Categoría" (change)="query.setFilter('categoryId', +$any($event.target).value || null)">
          <option value="">Todas las categorías</option>
          @for (c of categories.value() ?? []; track c.id) {
            <option [value]="c.id" [selected]="query.filters().categoryId === c.id">{{ c.name }}</option>
          }
        </select>
        <label class="check">
          <input type="checkbox" [checked]="query.filters().lowStock" (change)="query.setFilter('lowStock', $any($event.target).checked || null)" />
          Solo stock bajo
        </label>
        <label class="check">
          <input type="checkbox" [checked]="query.filters().active === false" (change)="query.setFilter('active', $any($event.target).checked ? false : true)" />
          Ver inactivos
        </label>
      </div>

      <div class="table-wrap" [class.loading]="products.isLoading()">
        <table class="table">
          <thead>
            <tr>
              <th><app-sort-button key="sku" label="SKU" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">SKU</app-sort-button></th>
              <th><app-sort-button key="name" label="nombre" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Producto</app-sort-button></th>
              <th><app-sort-button key="category" label="categoría" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Categoría</app-sort-button></th>
              <th class="num"><app-sort-button key="price" label="precio" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Precio neto</app-sort-button></th>
              <th>IVA</th>
              <th class="num"><app-sort-button key="stock" label="stock" [sort]="query.sort()" (sortChange)="query.toggleSort($event)">Stock</app-sort-button></th>
              <th class="num">Mínimo</th>
            </tr>
          </thead>
          <tbody>
            @for (p of products.value()?.items ?? []; track p.id) {
              <tr [class.inactive]="!p.isActive">
                <td><code>{{ p.sku }}</code></td>
                <td><a [routerLink]="[p.id]">{{ p.name }}</a></td>
                <td><span class="chip" [style.--chip]="p.categoryColor">{{ p.categoryName }}</span></td>
                <td class="num">{{ p.price | ars }}</td>
                <td>{{ vat[p.vatRate] }}</td>
                <td class="num">
                  @if (p.isLowStock) { <span class="badge badge-danger" title="Por debajo del mínimo">⚠ {{ p.totalStock }}</span> }
                  @else { {{ p.totalStock }} }
                </td>
                <td class="num muted">{{ p.minimumStock }}</td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="empty">No hay productos que coincidan con los filtros.</td></tr>
            }
          </tbody>
        </table>
      </div>

      @if (products.value(); as page) {
        <app-pagination [page]="page.page" [totalPages]="page.totalPages" [totalCount]="page.totalCount" (pageChange)="query.page.set($event)" />
      }
    </section>
  `,
})
export class ProductListPage implements OnInit {
  private readonly api = inject(ApiService);
  protected readonly canEdit = inject(AuthService).hasRole('admin', 'deposito');
  protected readonly vat = VAT_LABELS;

  /** `/productos?lowStock=true` (dashboard links) */
  readonly lowStock = input<string>();

  protected readonly query = new ListQuery<ProductFilters>({ sort: 'name', filters: { categoryId: null, lowStock: null, active: true } });
  protected readonly products = httpResource<Paged<ProductListItem>>(() => ({ url: '/api/products', params: this.query.params() }));
  protected readonly categories = httpResource<Category[]>(() => '/api/categories');

  ngOnInit(): void {
    if (this.lowStock() === 'true') {
      this.query.setFilter('lowStock', true);
    }
  }

  exportCsv(): void {
    this.api.download('/api/products/export', 'productos.csv', this.query.params().delete('page').delete('pageSize')).subscribe();
  }
}
