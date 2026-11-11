import { areaPath, donutSegment, linePath, linearScale, niceTicks, pieAngles, polar } from './chart-utils';

describe('chart-utils', () => {
  it('linearScale maps domain to range (inverted y axis too)', () => {
    const y = linearScale(0, 100, 200, 0);
    expect(y(0)).toBe(200);
    expect(y(50)).toBe(100);
    expect(y(100)).toBe(0);
  });

  it('linearScale does not divide by zero on an empty domain', () => {
    expect(linearScale(5, 5, 0, 10)(5)).toBe(0);
  });

  it('niceTicks produces round steps that cover the max', () => {
    expect(niceTicks(87, 4)).toEqual([0, 25, 50, 75, 100]);
    expect(niceTicks(1_234_567, 4)).toEqual([0, 500_000, 1_000_000, 1_500_000]);
    expect(niceTicks(0)).toEqual([0, 1]);
  });

  it('builds line and area paths', () => {
    const pts = [{ x: 0, y: 10 }, { x: 5, y: 2.555 }];
    expect(linePath(pts)).toBe('M0,10 L5,2.56');
    expect(areaPath(pts, 20)).toBe('M0,10 L5,2.56 L5,20 L0,20 Z');
    expect(areaPath([], 20)).toBe('');
  });

  it('polar measures angles clockwise from 12 o\'clock', () => {
    const top = polar(100, 100, 50, 0);
    const right = polar(100, 100, 50, Math.PI / 2);
    expect(top.x).toBeCloseTo(100);
    expect(top.y).toBeCloseTo(50);
    expect(right.x).toBeCloseTo(150);
    expect(right.y).toBeCloseTo(100);
  });

  it('pieAngles splits the circle proportionally and ignores negatives', () => {
    const angles = pieAngles([1, 3, -2]);
    expect(angles[0].fraction).toBe(0.25);
    expect(angles[1].fraction).toBe(0.75);
    expect(angles[2].fraction).toBe(0);
    expect(angles[1].end).toBeCloseTo(Math.PI * 2);
  });

  it('donutSegment uses the large-arc flag past 180° and never draws a degenerate full circle', () => {
    expect(donutSegment(0, 0, 10, 5, 0, Math.PI / 2)).toContain(' 0 0 1 ');
    expect(donutSegment(0, 0, 10, 5, 0, Math.PI * 1.5)).toContain(' 0 1 1 ');
    const full = donutSegment(0, 0, 10, 5, 0, Math.PI * 2);
    expect(full).not.toContain('NaN');
    expect(full.startsWith('M0,-10')).toBe(true);
  });
});
