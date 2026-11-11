import { VAT_FACTORS } from '../../core/labels';
import { VatRate } from '../../core/models';

export interface DraftLine {
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  vatRate: VatRate;
}

export interface LineAmounts {
  gross: number;
  discount: number;
  net: number;
  vat: number;
  total: number;
}

export interface InvoiceTotals {
  lines: LineAmounts[];
  subtotal: number;
  discountTotal: number;
  netTotal: number;
  vat21: number;
  vat105: number;
  total: number;
}

/** Commercial rounding to cents (half away from zero), working in integer cents to avoid float drift. */
export function round2(value: number): number {
  const cents = Math.abs(value) * 100;
  const rounded = Math.round(Number(cents.toFixed(6)));
  return (Math.sign(value) * rounded) / 100;
}

/**
 * Live preview of the invoice totals. Mirrors the domain rules of the API (Invoice.Recalculate):
 * line discount → invoice discount → VAT per rate, rounding each step to cents.
 * The server always recalculates; this is only for instant feedback while typing.
 */
export function calculateTotals(lines: DraftLine[], invoiceDiscountPercent: number): InvoiceTotals {
  const amounts = lines.map((l) => {
    const gross = round2((l.quantity || 0) * (l.unitPrice || 0));
    const lineDiscount = round2((gross * (l.discountPercent || 0)) / 100);
    const globalDiscount = round2(((gross - lineDiscount) * (invoiceDiscountPercent || 0)) / 100);
    const discount = round2(lineDiscount + globalDiscount);
    const net = round2(gross - discount);
    const vat = round2(net * VAT_FACTORS[l.vatRate]);
    return { gross, discount, net, vat, total: round2(net + vat) };
  });

  const sum = (pick: (a: LineAmounts) => number, filter: (i: number) => boolean = () => true) =>
    round2(amounts.reduce((acc, a, i) => (filter(i) ? acc + pick(a) : acc), 0));

  const netTotal = sum((a) => a.net);
  const vat21 = sum((a) => a.vat, (i) => lines[i].vatRate === 'General');
  const vat105 = sum((a) => a.vat, (i) => lines[i].vatRate === 'Reducido');

  return {
    lines: amounts,
    subtotal: sum((a) => a.gross),
    discountTotal: sum((a) => a.discount),
    netTotal,
    vat21,
    vat105,
    total: round2(netTotal + vat21 + vat105),
  };
}
