import { TestBed } from '@angular/core/testing';
import { PaginationComponent } from './pagination.component';

describe('PaginationComponent', () => {
  async function render(page: number, totalPages: number) {
    const fixture = TestBed.createComponent(PaginationComponent);
    fixture.componentRef.setInput('page', page);
    fixture.componentRef.setInput('totalPages', totalPages);
    fixture.componentRef.setInput('totalCount', totalPages * 15);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const buttons = Array.from(el.querySelectorAll('button'));
    return { fixture, buttons, numbers: buttons.slice(1, -1).map((b) => b.textContent?.trim()) };
  }

  it('shows a window of 5 pages around the current one', async () => {
    expect((await render(1, 10)).numbers).toEqual(['1', '2', '3', '4', '5']);
    expect((await render(6, 10)).numbers).toEqual(['4', '5', '6', '7', '8']);
    expect((await render(10, 10)).numbers).toEqual(['6', '7', '8', '9', '10']);
    expect((await render(1, 2)).numbers).toEqual(['1', '2']);
  });

  it('disables prev/next at the edges and marks the current page', async () => {
    const { buttons } = await render(1, 3);
    expect(buttons[0].disabled).toBe(true);
    expect(buttons.at(-1)!.disabled).toBe(false);
    expect(buttons[1].getAttribute('aria-current')).toBe('page');
  });

  it('emits the requested page', async () => {
    const { fixture, buttons } = await render(2, 5);
    const emitted: number[] = [];
    fixture.componentInstance.pageChange.subscribe((p) => emitted.push(p));
    buttons.at(-1)!.click();
    buttons[0].click();
    buttons[4].click(); // page numbers 1..5 → "4"
    expect(emitted).toEqual([3, 1, 4]);
  });
});
