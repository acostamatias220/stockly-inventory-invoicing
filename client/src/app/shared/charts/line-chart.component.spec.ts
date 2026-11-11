import { TestBed } from '@angular/core/testing';
import { LineChartComponent } from './line-chart.component';

describe('LineChartComponent', () => {
  it('draws the line, the area and one hit target per point', async () => {
    const fixture = TestBed.createComponent(LineChartComponent);
    fixture.componentRef.setInput('data', [
      { label: '01/11', value: 100 },
      { label: '02/11', value: 400 },
      { label: '03/11', value: 250 },
    ]);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    const line = el.querySelector('path.line')?.getAttribute('d') ?? '';
    expect(line.startsWith('M')).toBe(true);
    expect(line.split('L').length).toBe(3);
    expect(el.querySelector('path.area')?.getAttribute('d')).toMatch(/Z$/);
    expect(el.querySelectorAll('rect.hit').length).toBe(3);
    expect(el.querySelectorAll('g.grid').length).toBeGreaterThan(1);
  });

  it('shows a tooltip for the hovered point', async () => {
    const fixture = TestBed.createComponent(LineChartComponent);
    fixture.componentRef.setInput('data', [
      { label: 'lun', value: 10 },
      { label: 'mar', value: 20 },
    ]);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    el.querySelectorAll('rect.hit')[1].dispatchEvent(new Event('mouseenter'));
    await fixture.whenStable();
    expect(el.querySelector('.tooltip')?.textContent).toContain('mar');
  });
});
