/**
 * Small, dependency-free helpers for the hand-made SVG charts. Kept as pure functions so they are
 * trivially unit-testable and the components only deal with layout and interaction.
 */

/** Linear scale: maps a value in [d0, d1] to [r0, r1]. */
export function linearScale(d0: number, d1: number, r0: number, r1: number): (v: number) => number {
  const span = d1 - d0 || 1;
  return (v: number) => r0 + ((v - d0) / span) * (r1 - r0);
}

/**
 * "Nice" axis ticks (1, 2, 2.5, 5 × 10ⁿ) from 0 to at least `max`, Excel/D3 style.
 * e.g. niceTicks(87, 4) → [0, 25, 50, 75, 100]
 */
export function niceTicks(max: number, count = 4): number[] {
  if (!Number.isFinite(max) || max <= 0) return [0, 1];
  const rough = max / count;
  const magnitude = 10 ** Math.floor(Math.log10(rough));
  const step = [1, 2, 2.5, 5, 10].map((m) => m * magnitude).find((s) => s >= rough) ?? 10 * magnitude;
  const ticks: number[] = [];
  for (let v = 0; v < max + step - 1e-9; v += step) {
    ticks.push(Number(v.toFixed(10)));
  }
  return ticks;
}

/** SVG path through the points ("M x,y L x,y …"). */
export function linePath(points: readonly { x: number; y: number }[]): string {
  return points.map((p, i) => `${i === 0 ? 'M' : 'L'}${round(p.x)},${round(p.y)}`).join(' ');
}

/** Closed area under a line down to `baseline`. */
export function areaPath(points: readonly { x: number; y: number }[], baseline: number): string {
  if (points.length === 0) return '';
  const first = points[0];
  const last = points[points.length - 1];
  return `${linePath(points)} L${round(last.x)},${round(baseline)} L${round(first.x)},${round(baseline)} Z`;
}

/** Point on a circle; angle in radians measured clockwise from 12 o'clock. */
export function polar(cx: number, cy: number, r: number, angle: number): { x: number; y: number } {
  return { x: cx + r * Math.sin(angle), y: cy - r * Math.cos(angle) };
}

/**
 * Donut segment between two angles (radians, clockwise from 12 o'clock) as a single closed path:
 * outer arc → line to inner radius → inner arc back → close.
 */
export function donutSegment(cx: number, cy: number, outer: number, inner: number, start: number, end: number): string {
  // A full 360° arc can't be drawn with one arc command (start == end); stop just short of it.
  const sweep = Math.min(end - start, Math.PI * 2 - 1e-4);
  const finish = start + sweep;
  const large = sweep > Math.PI ? 1 : 0;
  const o0 = polar(cx, cy, outer, start);
  const o1 = polar(cx, cy, outer, finish);
  const i1 = polar(cx, cy, inner, finish);
  const i0 = polar(cx, cy, inner, start);
  return [
    `M${round(o0.x)},${round(o0.y)}`,
    `A${outer},${outer} 0 ${large} 1 ${round(o1.x)},${round(o1.y)}`,
    `L${round(i1.x)},${round(i1.y)}`,
    `A${inner},${inner} 0 ${large} 0 ${round(i0.x)},${round(i0.y)}`,
    'Z',
  ].join(' ');
}

/** Splits 2π proportionally to the values: [{start, end, fraction}]. Zero/negative values get no arc. */
export function pieAngles(values: readonly number[]): { start: number; end: number; fraction: number }[] {
  const total = values.reduce((acc, v) => acc + Math.max(0, v), 0);
  let cursor = 0;
  return values.map((v) => {
    const fraction = total > 0 ? Math.max(0, v) / total : 0;
    const start = cursor;
    cursor += fraction * Math.PI * 2;
    return { start, end: cursor, fraction };
  });
}

function round(n: number): number {
  return Math.round(n * 100) / 100;
}
