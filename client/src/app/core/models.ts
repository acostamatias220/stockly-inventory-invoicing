export type Role = 'admin' | 'vendedor' | 'deposito';
export type UserRoleName = 'Admin' | 'Vendedor' | 'Deposito';
export type VatRate = 'Exento' | 'Reducido' | 'General';
export type TaxCondition = 'ResponsableInscripto' | 'Monotributista' | 'ConsumidorFinal' | 'Exento';
export type InvoiceLetter = 'A' | 'B';
export type InvoiceStatus = 'Draft' | 'Issued' | 'Paid' | 'Cancelled';
export type PaymentMethod = 'Efectivo' | 'Transferencia' | 'Tarjeta' | 'MercadoPago';
export type StockMovementType = 'Entrada' | 'Salida' | 'Transferencia' | 'Ajuste' | 'Venta' | 'AnulacionVenta';

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface User {
  id: number;
  email: string;
  fullName: string;
  role: Role;
  isActive: boolean;
  lastLoginAt: string | null;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: User;
}

export interface Category {
  id: number;
  name: string;
  description: string | null;
  color: string;
  productCount: number;
}

export interface Warehouse {
  id: number;
  code: string;
  name: string;
  address: string | null;
  isActive: boolean;
}

export interface ProductListItem {
  id: number;
  sku: string;
  name: string;
  categoryId: number;
  categoryName: string;
  categoryColor: string;
  price: number;
  cost: number;
  vatRate: VatRate;
  minimumStock: number;
  totalStock: number;
  isLowStock: boolean;
  isActive: boolean;
}

export interface WarehouseStock {
  warehouseId: number;
  warehouseCode: string;
  warehouseName: string;
  quantity: number;
}

export interface ProductDetail extends Omit<ProductListItem, 'categoryColor'> {
  description: string | null;
  supplierId: number | null;
  supplierName: string | null;
  stock: WarehouseStock[];
  createdAt: string;
  updatedAt: string;
}

export interface LowStockItem {
  id: number;
  sku: string;
  name: string;
  categoryName: string;
  totalStock: number;
  minimumStock: number;
}

export interface StockMovement {
  id: number;
  createdAt: string;
  type: StockMovementType;
  productId: number;
  sku: string;
  productName: string;
  warehouseId: number;
  warehouseCode: string;
  quantity: number;
  balanceAfter: number;
  reason: string | null;
  reference: string | null;
  invoiceId: number | null;
  supplierName: string | null;
  userName: string;
  correlationId: string;
}

export interface Customer {
  id: number;
  name: string;
  taxId: string;
  taxCondition: TaxCondition;
  invoiceLetter: InvoiceLetter;
  email: string | null;
  phone: string | null;
  address: string | null;
  isActive: boolean;
  balance: number;
}

export interface Supplier {
  id: number;
  name: string;
  taxId: string;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  address: string | null;
  isActive: boolean;
  productCount: number;
}

export interface InvoiceListItem {
  id: number;
  number: string | null;
  letter: InvoiceLetter;
  status: InvoiceStatus;
  customerId: number;
  customerName: string;
  issueDate: string | null;
  dueDate: string;
  total: number;
  isOverdue: boolean;
  createdAt: string;
}

export interface InvoiceLine {
  id: number;
  productId: number;
  sku: string;
  description: string;
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  vatRate: VatRate;
  grossAmount: number;
  discountAmount: number;
  netAmount: number;
  vatAmount: number;
  total: number;
}

export interface InvoiceDetail {
  id: number;
  number: string | null;
  letter: InvoiceLetter;
  pointOfSale: number;
  status: InvoiceStatus;
  customer: { id: number; name: string; taxId: string; taxCondition: TaxCondition; address: string | null; email: string | null };
  warehouseId: number;
  warehouseName: string;
  issueDate: string | null;
  dueDate: string;
  discountPercent: number;
  notes: string | null;
  lines: InvoiceLine[];
  subtotal: number;
  discountTotal: number;
  netTotal: number;
  vat21: number;
  vat105: number;
  total: number;
  paymentMethod: PaymentMethod | null;
  paidAt: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
  version: number;
  isOverdue: boolean;
}

export interface SaveInvoiceRequest {
  customerId: number;
  warehouseId: number;
  dueDate: string | null;
  discountPercent: number;
  notes: string | null;
  lines: { productId: number; quantity: number; unitPrice: number | null; discountPercent: number }[];
  version?: number;
}

export interface Dashboard {
  kpis: {
    salesThisMonth: number;
    salesPreviousMonth: number;
    invoicesThisMonth: number;
    receivables: number;
    overdueInvoices: number;
    inventoryValue: number;
    lowStockProducts: number;
    activeProducts: number;
  };
  salesByDay: { date: string; total: number; invoices: number }[];
  topProducts: { productId: number; sku: string; name: string; quantity: number; revenue: number }[];
  stockByCategory: { categoryId: number; name: string; color: string; units: number; value: number }[];
  lowStock: LowStockItem[];
}

/** RFC 7807 body returned by the API on errors. */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  code?: string;
  errors?: Record<string, string[]>;
}
