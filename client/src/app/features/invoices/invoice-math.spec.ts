import { calculateTotals, round2 } from './invoice-math';

describe('invoice-math', () => {
  it('rounds half away from zero to cents', () => {
    expect(round2(1.005)).toBe(1.01);
    expect(round2(2.675)).toBe(2.68);
    expect(round2(-1.005)).toBe(-1.01);
    expect(round2(10)).toBe(10);
  });

  it('applies VAT 21% and 10.5% separately', () => {
    const totals = calculateTotals(
      [
        { quantity: 2, unitPrice: 1000, discountPercent: 0, vatRate: 'General' },
        { quantity: 1, unitPrice: 83.4, discountPercent: 0, vatRate: 'Reducido' },
      ],
      0,
    );
    expect(totals.netTotal).toBe(2083.4);
    expect(totals.vat21).toBe(420);
    expect(totals.vat105).toBe(8.76);
    expect(totals.total).toBe(2512.16);
  });

  it('applies line discount before the invoice discount and both before VAT', () => {
    const totals = calculateTotals([{ quantity: 1, unitPrice: 1000, discountPercent: 10, vatRate: 'General' }], 5);
    // 1000 - 10% = 900; 900 - 5% = 855; IVA 21% = 179.55
    expect(totals.lines[0]).toEqual({ gross: 1000, discount: 145, net: 855, vat: 179.55, total: 1034.55 });
    expect(totals.subtotal).toBe(1000);
    expect(totals.discountTotal).toBe(145);
    expect(totals.total).toBe(1034.55);
  });

  it('exempt lines add no VAT', () => {
    const totals = calculateTotals([{ quantity: 3, unitPrice: 50, discountPercent: 0, vatRate: 'Exento' }], 0);
    expect(totals.vat21 + totals.vat105).toBe(0);
    expect(totals.total).toBe(150);
  });

  it('treats empty inputs as zero', () => {
    const totals = calculateTotals([{ quantity: NaN, unitPrice: 0, discountPercent: 0, vatRate: 'General' }], 0);
    expect(totals.total).toBe(0);
  });
});
