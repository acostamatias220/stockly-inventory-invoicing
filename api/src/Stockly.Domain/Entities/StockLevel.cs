namespace Stockly.Domain.Entities;

/// <summary>
/// Current quantity of a product in a warehouse (composite key). It is a projection of the
/// stock movements ledger and is only modified through atomic SQL updates inside a transaction.
/// </summary>
public class StockLevel
{
    private StockLevel() { }

    public StockLevel(int productId, int warehouseId, int quantity)
    {
        ProductId = productId;
        WarehouseId = warehouseId;
        Quantity = quantity;
    }

    public int ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public int WarehouseId { get; private set; }
    public Warehouse Warehouse { get; private set; } = null!;
    public int Quantity { get; private set; }
    public DateTime UpdatedAt { get; private set; }
}
