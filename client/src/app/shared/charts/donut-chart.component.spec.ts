import { TestBed } from '@angular/core/testing';
import { DonutChartComponent } from './donut-chart.component';

describe('DonutChartComponent', () => {
  async function render(data: { label: string; value: number; color: string }[]) {
    const fixture = TestBed.createComponent(DonutChartComponent);
    fixture.componentRef.setInput('data', data);
    fixture.componentRef.setInput('totalLabel', 'Valor total');
    await fixture.whenStable();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('renders one path per segment with its colour and a legend with percentages', async () => {
    const { el } = await render([
      { label: 'Herramientas', value: 300, color: '#ff0000' },
      { label: 'Electricidad', value: 100, color: '#00ff00' },
    ]);
    const paths = el.querySelectorAll('path.segment');
    expect(paths.length).toBe(2);
    expect(paths[0].getAttribute('fill')).toBe('#ff0000');
    const legend = Array.from(el.querySelectorAll('.legend-value')).map((n) => n.textContent?.trim());
    expect(legend).toEqual(['75%', '25%']);
    expect(el.querySelector('.center-label')?.textContent).toContain('Valor total');
  });

  it('shows the hovered segment in the center', async () => {
    const { fixture, el } = await render([
      { label: 'A', value: 1, color: '#000' },
      { label: 'B', value: 1, color: '#fff' },
    ]);
    el.querySelectorAll('.legend li')[1].dispatchEvent(new Event('mouseenter'));
    await fixture.whenStable();
    expect(el.querySelector('.center-label')?.textContent).toContain('B');
    expect(el.querySelectorAll('path.dimmed').length).toBe(1);
  });
});
