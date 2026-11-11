namespace Stockly.Domain.Enums;

public enum UserRole
{
    Admin = 1,
    Vendedor = 2,
    Deposito = 3,
}

/// <summary>Argentine VAT (IVA) rates. The numeric value is the rate in per-mille to keep it an integer.</summary>
public enum VatRate
{
    Exento = 0,
    Reducido = 105,
    General = 210,
}

/// <summary>Customer's position before AFIP/ARCA. Determines the invoice letter (A or B).</summary>
public enum TaxCondition
{
    ResponsableInscripto = 1,
    Monotributista = 2,
    ConsumidorFinal = 3,
    Exento = 4,
}

public enum InvoiceLetter
{
    A = 1,
    B = 2,
}

public enum InvoiceStatus
{
    Draft = 1,
    Issued = 2,
    Paid = 3,
    Cancelled = 4,
}

public enum PaymentMethod
{
    Efectivo = 1,
    Transferencia = 2,
    Tarjeta = 3,
    MercadoPago = 4,
}

public enum StockMovementType
{
    /// <summary>Goods received (purchase, return from customer…).</summary>
    Entrada = 1,

    /// <summary>Manual exit (breakage, internal use…).</summary>
    Salida = 2,

    /// <summary>One leg of a transfer between warehouses. Both legs share a correlation id.</summary>
    Transferencia = 3,

    /// <summary>Physical count correction. Quantity is the delta applied.</summary>
    Ajuste = 4,

    /// <summary>Automatic exit generated when an invoice is issued.</summary>
    Venta = 5,

    /// <summary>Automatic re-entry generated when an issued invoice is cancelled.</summary>
    AnulacionVenta = 6,
}

public static class VatRateExtensions
{
    public static decimal ToFactor(this VatRate rate) => (int)rate / 1000m;

    public static string ToLabel(this VatRate rate) => rate switch
    {
        VatRate.General => "21%",
        VatRate.Reducido => "10,5%",
        _ => "Exento",
    };
}

public static class TaxConditionExtensions
{
    /// <summary>Responsable Inscripto receives "Factura A"; everybody else "Factura B".</summary>
    public static InvoiceLetter ToInvoiceLetter(this TaxCondition condition) =>
        condition == TaxCondition.ResponsableInscripto ? InvoiceLetter.A : InvoiceLetter.B;
}
