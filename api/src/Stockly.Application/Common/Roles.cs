namespace Stockly.Application.Common;

/// <summary>Role names as they travel in the JWT and authorization policies.</summary>
public static class Roles
{
    public const string Admin = "admin";
    public const string Vendedor = "vendedor";
    public const string Deposito = "deposito";

    public static string From(Domain.Enums.UserRole role) => role switch
    {
        Domain.Enums.UserRole.Admin => Admin,
        Domain.Enums.UserRole.Vendedor => Vendedor,
        Domain.Enums.UserRole.Deposito => Deposito,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };
}

/// <summary>Authorization policies (who can do what) shared by the API and the docs.</summary>
public static class Policies
{
    /// <summary>admin + deposito: products, categories, warehouses, suppliers, stock movements.</summary>
    public const string ManageInventory = nameof(ManageInventory);

    /// <summary>admin + vendedor: customers and invoices.</summary>
    public const string Sell = nameof(Sell);

    /// <summary>admin only: users, cancelling issued invoices.</summary>
    public const string AdminOnly = nameof(AdminOnly);
}
