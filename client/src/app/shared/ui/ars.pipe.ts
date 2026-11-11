import { Pipe, PipeTransform } from '@angular/core';

const formatter = new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' });
const compact = new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', notation: 'compact', maximumFractionDigits: 1 });

/** `1234.5 | ars` → "$ 1.234,50" · `1234567 | ars:'compact'` → "$ 1,2 M". */
@Pipe({ name: 'ars' })
export class ArsPipe implements PipeTransform {
  transform(value: number | null | undefined, mode: 'full' | 'compact' = 'full'): string {
    if (value === null || value === undefined) return '—';
    return (mode === 'compact' ? compact : formatter).format(value);
  }
}

export const formatArs = (value: number) => formatter.format(value);
export const formatArsCompact = (value: number) => compact.format(value);
