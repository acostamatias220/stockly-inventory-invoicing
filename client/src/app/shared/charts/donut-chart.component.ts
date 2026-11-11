import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { formatArs, formatArsCompact } from '../ui/ars.pipe';
import { donutSegment, pieAngles } from './chart-utils';

export interface DonutDatum {
  label: string;
  value: number;
  color: string;
  hint?: string;
}

const SIZE = 220;
const OUTER = 100;
const INNER = 64;

/** Donut chart with legend; hovering a segment or legend item highlights it and updates the center text. */
@Component({
  selector: 'app-donut-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './charts.scss',
  template: `
    <figure class="chart donut">
      <svg [attr.viewBox]="'0 0 ' + size + ' ' + size" role="img" [attr.aria-label]="ariaLabel()">
        @for (s of segments(); track s.label) {
          <path class="segment" [attr.d]="s.path" [attr.fill]="s.color" [class.dimmed]="active() !== null && active() !== $index"
            (mouseenter)="active.set($index)" (mouseleave)="active.set(null)">
            <title>{{ s.label }}: {{ money(s.value) }} ({{ s.percent }}%)</title>
          </path>
        }
        <text class="center-value" [attr.x]="size / 2" [attr.y]="size / 2 - 4" text-anchor="middle">{{ centerValue() }}</text>
        <text class="center-label" [attr.x]="size / 2" [attr.y]="size / 2 + 16" text-anchor="middle">{{ centerLabel() }}</text>
      </svg>
      <ul class="legend">
        @for (s of segments(); track s.label) {
          <li (mouseenter)="active.set($index)" (mouseleave)="active.set(null)" [class.active]="active() === $index">
            <span class="swatch" [style.background]="s.color"></span>
            <span class="legend-label">{{ s.label }}</span>
            <span class="legend-value">{{ s.percent }}%</span>
          </li>
        }
      </ul>
    </figure>
  `,
})
export class DonutChartComponent {
  readonly data = input.required<DonutDatum[]>();
  readonly totalLabel = input('Total');
  readonly ariaLabel = input('Gráfico de anillo');

  protected readonly size = SIZE;
  protected readonly active = signal<number | null>(null);
  protected readonly money = formatArs;

  protected readonly total = computed(() => this.data().reduce((acc, d) => acc + d.value, 0));

  protected readonly segments = computed(() => {
    const data = this.data();
    const angles = pieAngles(data.map((d) => d.value));
    return data.map((d, i) => ({
      ...d,
      percent: Math.round(angles[i].fraction * 1000) / 10,
      path: donutSegment(SIZE / 2, SIZE / 2, OUTER, INNER, angles[i].start, angles[i].end),
    }));
  });

  protected readonly centerValue = computed(() => {
    const i = this.active();
    return formatArsCompact(i === null ? this.total() : this.segments()[i].value);
  });

  protected readonly centerLabel = computed(() => {
    const i = this.active();
    return i === null ? this.totalLabel() : this.segments()[i].label;
  });
}
