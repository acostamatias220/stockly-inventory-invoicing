import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { formatArs, formatArsCompact } from '../ui/ars.pipe';
import { areaPath, linePath, linearScale, niceTicks } from './chart-utils';

export interface LinePoint {
  label: string;
  value: number;
}

const W = 720;
const H = 260;
const M = { top: 16, right: 16, bottom: 28, left: 64 };

/**
 * Area/line chart in pure SVG. Scales are computed signals, so new data recomputes only the geometry.
 * The viewBox keeps it responsive; one invisible hit-area per point drives the tooltip.
 */
@Component({
  selector: 'app-line-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './charts.scss',
  template: `
    <figure class="chart">
      <svg [attr.viewBox]="'0 0 ' + width + ' ' + height" role="img" [attr.aria-label]="ariaLabel()" preserveAspectRatio="xMidYMid meet">
        <defs>
          <linearGradient [attr.id]="gradientId" x1="0" x2="0" y1="0" y2="1">
            <stop offset="0%" [attr.stop-color]="color()" stop-opacity="0.28" />
            <stop offset="100%" [attr.stop-color]="color()" stop-opacity="0" />
          </linearGradient>
        </defs>

        @for (tick of ticks(); track tick) {
          <g class="grid">
            <line [attr.x1]="margin.left" [attr.x2]="width - margin.right" [attr.y1]="y()(tick)" [attr.y2]="y()(tick)" />
            <text [attr.x]="margin.left - 8" [attr.y]="y()(tick)" text-anchor="end" dominant-baseline="middle">{{ compact(tick) }}</text>
          </g>
        }

        @for (label of xLabels(); track label.index) {
          <text class="axis-label" [attr.x]="label.x" [attr.y]="height - 8" text-anchor="middle">{{ label.text }}</text>
        }

        <path class="area" [attr.d]="area()" [attr.fill]="'url(#' + gradientId + ')'" />
        <path class="line" [attr.d]="line()" [attr.stroke]="color()" />

        @if (active(); as a) {
          <line class="guide" [attr.x1]="a.x" [attr.x2]="a.x" [attr.y1]="margin.top" [attr.y2]="height - margin.bottom" />
          <circle class="dot" [attr.cx]="a.x" [attr.cy]="a.y" r="5" [attr.fill]="color()" />
        }

        @for (p of points(); track $index) {
          <rect class="hit" [attr.x]="p.x - step() / 2" [attr.y]="margin.top" [attr.width]="step()"
            [attr.height]="height - margin.top - margin.bottom" (mouseenter)="activeIndex.set($index)" (mouseleave)="activeIndex.set(null)">
            <title>{{ p.label }}: {{ money(p.value) }}</title>
          </rect>
        }
      </svg>
      @if (active(); as a) {
        <div class="tooltip" [style.left.%]="(a.x / width) * 100" [style.top.%]="(a.y / height) * 100">
          <strong>{{ money(a.value) }}</strong><span>{{ a.label }}</span>
        </div>
      }
    </figure>
  `,
})
export class LineChartComponent {
  readonly data = input.required<LinePoint[]>();
  readonly color = input('#4F46E5');
  readonly ariaLabel = input('Gráfico de líneas');

  protected readonly width = W;
  protected readonly height = H;
  protected readonly margin = M;
  protected readonly gradientId = `line-gradient-${Math.random().toString(36).slice(2, 8)}`;
  protected readonly activeIndex = signal<number | null>(null);
  protected readonly money = formatArs;
  protected readonly compact = formatArsCompact;

  protected readonly ticks = computed(() => niceTicks(Math.max(...this.data().map((d) => d.value), 0)));
  protected readonly y = computed(() => linearScale(0, this.ticks().at(-1)!, H - M.bottom, M.top));
  protected readonly x = computed(() => linearScale(0, Math.max(1, this.data().length - 1), M.left, W - M.right));
  protected readonly step = computed(() => (W - M.left - M.right) / Math.max(1, this.data().length - 1));

  protected readonly points = computed(() => {
    const x = this.x();
    const y = this.y();
    return this.data().map((d, i) => ({ ...d, x: x(i), y: y(d.value) }));
  });

  protected readonly line = computed(() => linePath(this.points()));
  protected readonly area = computed(() => areaPath(this.points(), H - M.bottom));

  /** ~6 evenly spaced x labels regardless of the number of points. */
  protected readonly xLabels = computed(() => {
    const pts = this.points();
    const every = Math.max(1, Math.ceil(pts.length / 6));
    return pts.map((p, index) => ({ index, x: p.x, text: p.label })).filter((_, i) => i % every === 0 || i === pts.length - 1);
  });

  protected readonly active = computed(() => {
    const i = this.activeIndex();
    return i === null ? null : (this.points()[i] ?? null);
  });
}
