namespace Stockly.Domain.Common;

/// <summary>A business rule was violated. Mapped to HTTP 422 by the API.</summary>
public class DomainException : Exception
{
    public DomainException(string code, string message) : base(message) => Code = code;

    public string Code { get; }
}

/// <summary>Raised when an operation would leave a warehouse with negative stock.</summary>
public sealed class InsufficientStockException : DomainException
{
    public InsufficientStockException(string sku, string warehouse, int requested, int available)
        : base("stock.insufficient",
            $"Stock insuficiente de {sku} en {warehouse}: se pidieron {requested} y hay {available}.")
    {
        Sku = sku;
        Requested = requested;
        Available = available;
    }

    public string Sku { get; }
    public int Requested { get; }
    public int Available { get; }
}
