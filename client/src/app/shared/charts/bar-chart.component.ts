import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { formatArs, formatArsCompact } from '../ui/ars.pipe';
import { linearScale } from './chart-utils';

export interface BarDatum {
  label: string;
  value: number;
  hint?: string;
}

const W = 520;
const ROW = 34;
const LABEL = 190;
const VALUE = 80;

/** Horizontal bar chart (ranking). Height grows with the number of rows; bars animate via CSS. */
@Component({
  selector: 'app-bar-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './charts.scss',
  template: `
    <figure class="chart">
      <svg [attr.viewBox]="'0 0 ' + width + ' ' + height()" role="img" [attr.aria-label]="ariaLabel()">
        @for (bar of bars(); track bar.label) {
          <g class="bar-row" [attr.transform]="'translate(0,' + bar.y + ')'">
            <text class="bar-label" x="0" [attr.y]="row / 2" dominant-baseline="middle">{{ bar.short }}</text>
            <rect class="bar-track" [attr.x]="labelWidth" y="7" [attr.width]="trackWidth" [attr.height]="row - 14" rx="5" />
            <rect class="bar" [attr.x]="labelWidth" y="7" [attr.width]="bar.width" [attr.height]="row - 14" rx="5" [attr.fill]="color()">
              <title>{{ bar.label }}: {{ money(bar.value) }}{{ bar.hint ? ' · ' + bar.hint : '' }}</title>
            </rect>
            <text class="bar-value" [attr.x]="width" [attr.y]="row / 2" text-anchor="end" dominant-baseline="middle">{{ compact(bar.value) }}</text>
          </g>
        } @empty {
          <text x="50%" y="50%" text-anchor="middle" class="axis-label">Sin datos en el período</text>
        }
      </svg>
    </figure>
  `,
})
export class BarChartComponent {
  readonly data = input.required<BarDatum[]>();
  readonly color = input('#0EA5E9');
  readonly ariaLabel = input('Gráfico de barras');

  protected readonly width = W;
  protected readonly row = ROW;
  protected readonly labelWidth = LABEL;
  protected readonly trackWidth = W - LABEL - VALUE;
  protected readonly money = formatArs;
  protected readonly compact = formatArsCompact;

  protected readonly height = computed(() => Math.max(ROW, this.data().length * ROW));

  protected readonly bars = computed(() => {
    const data = this.data();
    const scale = linearScale(0, Math.max(...data.map((d) => d.value), 1), 0, this.trackWidth);
    return data.map((d, i) => ({
      ...d,
      y: i * ROW,
      width: Math.max(2, scale(d.value)),
      short: d.label.length > 26 ? `${d.label.slice(0, 25)}…` : d.label,
    }));
  });
}
