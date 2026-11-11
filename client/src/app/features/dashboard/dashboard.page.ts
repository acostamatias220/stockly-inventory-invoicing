import { DecimalPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Dashboard } from '../../core/models';
import { BarChartComponent } from '../../shared/charts/bar-chart.component';
import { DonutChartComponent } from '../../shared/charts/donut-chart.component';
import { LineChartComponent } from '../../shared/charts/line-chart.component';
import { ArsPipe } from '../../shared/ui/ars.pipe';

const dayLabel = new Intl.DateTimeFormat('es-AR', { day: '2-digit', month: 'short', timeZone: 'UTC' });

@Component({
  selector: 'app-dashboard-page',
  imports: [ArsPipe, DecimalPipe, RouterLink, LineChartComponent, BarChartComponent, DonutChartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './dashboard.page.scss',
  template: `
    <header class="page-header">
      <div>
        <h1>Dashboard</h1>
        <p class="muted">Resumen de ventas e inventario</p>
      </div>
      <div class="segmented" role="group" aria-label="Período">
        @for (d of periods; track d) {
          <button type="button" [class.active]="days() === d" (click)="days.set(d)">{{ d }} días</button>
        }
      </div>
    </header>

    @if (dashboard.error()) {
      <p class="alert alert-error">No se pudo cargar el dashboard.</p>
    }

    @if (data(); as d) {
      <section class="kpis">
        <article class="card kpi">
          <span class="kpi-label">Ventas del mes</span>
          <strong class="kpi-value">{{ d.kpis.salesThisMonth | ars: 'compact' }}</strong>
          <span class="kpi-trend" [class.up]="trend() >= 0" [class.down]="trend() < 0" title="Mes en curso contra los mismos días del mes anterior">
            {{ trend() >= 0 ? '▲' : '▼' }} {{ trend() | number: '1.0-1' }}% vs. mismo período
          </span>
        </article>
        <article class="card kpi">
          <span class="kpi-label">Facturas emitidas (mes)</span>
          <strong class="kpi-value">{{ d.kpis.invoicesThisMonth }}</strong>
          <span class="muted">IVA incluido en los totales</span>
        </article>
        <article class="card kpi">
          <span class="kpi-label">A cobrar</span>
          <strong class="kpi-value">{{ d.kpis.receivables | ars: 'compact' }}</strong>
          <a class="kpi-link" routerLink="/facturas" [queryParams]="{ overdue: true }">{{ d.kpis.overdueInvoices }} vencidas →</a>
        </article>
        <article class="card kpi">
          <span class="kpi-label">Valor del inventario (costo)</span>
          <strong class="kpi-value">{{ d.kpis.inventoryValue | ars: 'compact' }}</strong>
          <a class="kpi-link warn" routerLink="/productos" [queryParams]="{ lowStock: true }">{{ d.kpis.lowStockProducts }} con stock bajo →</a>
        </article>
      </section>

      <section class="grid">
        <article class="card span-2">
          <header class="card-header"><h2>Ventas por día</h2><span class="muted">últimos {{ days() }} días</span></header>
          <app-line-chart [data]="salesSeries()" ariaLabel="Ventas diarias del período" />
        </article>

        <article class="card">
          <header class="card-header"><h2>Productos más vendidos</h2><span class="muted">neto facturado</span></header>
          <app-bar-chart [data]="topProducts()" ariaLabel="Ranking de productos por facturación" />
        </article>

        <article class="card">
          <header class="card-header"><h2>Stock por categoría</h2><span class="muted">valorizado a costo</span></header>
          <app-donut-chart [data]="stockByCategory()" totalLabel="Inventario" ariaLabel="Distribución del inventario por categoría" />
        </article>

        <article class="card span-2">
          <header class="card-header">
            <h2>Alertas de stock mínimo</h2>
            <a routerLink="/productos" [queryParams]="{ lowStock: true }">Ver todos</a>
          </header>
          @if (d.lowStock.length === 0) {
            <p class="muted">Todo en orden: ningún producto está por debajo de su mínimo. 🎉</p>
          } @else {
            <table class="table">
              <thead><tr><th>SKU</th><th>Producto</th><th>Categoría</th><th class="num">Stock</th><th class="num">Mínimo</th><th>Nivel</th></tr></thead>
              <tbody>
                @for (p of d.lowStock; track p.id) {
                  <tr>
                    <td><code>{{ p.sku }}</code></td>
                    <td><a [routerLink]="['/productos', p.id]">{{ p.name }}</a></td>
                    <td>{{ p.categoryName }}</td>
                    <td class="num danger">{{ p.totalStock }}</td>
                    <td class="num">{{ p.minimumStock }}</td>
                    <td><div class="meter" [attr.aria-label]="'Nivel ' + p.totalStock + ' de ' + p.minimumStock"><span [style.width.%]="(p.totalStock / p.minimumStock) * 100"></span></div></td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </article>
      </section>
    } @else if (dashboard.isLoading()) {
      <section class="kpis">@for (i of [1, 2, 3, 4]; track i) { <div class="card skeleton"></div> }</section>
    }
  `,
})
export class DashboardPage {
  protected readonly periods = [7, 30, 90];
  protected readonly days = signal(30);

  protected readonly dashboard = httpResource<Dashboard>(() => ({ url: '/api/dashboard', params: { days: this.days() } }));
  protected readonly data = computed(() => this.dashboard.value());

  protected readonly trend = computed(() => {
    const k = this.data()?.kpis;
    if (!k || k.salesPreviousMonth === 0) return 0;
    return ((k.salesThisMonth - k.salesPreviousMonth) / k.salesPreviousMonth) * 100;
  });

  protected readonly salesSeries = computed(() =>
    (this.data()?.salesByDay ?? []).map((p) => ({ label: dayLabel.format(new Date(p.date)), value: p.total })),
  );

  protected readonly topProducts = computed(() =>
    (this.data()?.topProducts ?? []).map((p) => ({ label: p.name, value: p.revenue, hint: `${p.quantity} u.` })),
  );

  protected readonly stockByCategory = computed(() =>
    (this.data()?.stockByCategory ?? []).map((c) => ({ label: c.name, value: c.value, color: c.color })),
  );
}
