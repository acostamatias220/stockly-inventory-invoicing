import { InvoiceStatus, PaymentMethod, Role, StockMovementType, TaxCondition, VatRate } from './models';

export const ROLE_LABELS: Record<Role, string> = {
  admin: 'Administrador',
  vendedor: 'Vendedor',
  deposito: 'Depósito',
};

export const INVOICE_STATUS_LABELS: Record<InvoiceStatus, string> = {
  Draft: 'Borrador',
  Issued: 'Emitida',
  Paid: 'Pagada',
  Cancelled: 'Anulada',
};

export const VAT_LABELS: Record<VatRate, string> = {
  General: '21%',
  Reducido: '10,5%',
  Exento: 'Exento',
};

export const VAT_FACTORS: Record<VatRate, number> = {
  General: 0.21,
  Reducido: 0.105,
  Exento: 0,
};

export const TAX_CONDITION_LABELS: Record<TaxCondition, string> = {
  ResponsableInscripto: 'Responsable Inscripto',
  Monotributista: 'Monotributista',
  ConsumidorFinal: 'Consumidor Final',
  Exento: 'Exento',
};

export const PAYMENT_METHOD_LABELS: Record<PaymentMethod, string> = {
  Efectivo: 'Efectivo',
  Transferencia: 'Transferencia',
  Tarjeta: 'Tarjeta',
  MercadoPago: 'Mercado Pago',
};

export const MOVEMENT_TYPE_LABELS: Record<StockMovementType, string> = {
  Entrada: 'Entrada',
  Salida: 'Salida',
  Transferencia: 'Transferencia',
  Ajuste: 'Ajuste',
  Venta: 'Venta',
  AnulacionVenta: 'Anulación de venta',
};

export function entries<K extends string>(labels: Record<K, string>): { value: K; label: string }[] {
  return (Object.keys(labels) as K[]).map((value) => ({ value, label: labels[value] }));
}
