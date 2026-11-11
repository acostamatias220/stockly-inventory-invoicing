using Stockly.Domain.Common;
using Stockly.Domain.Enums;

namespace Stockly.Domain.Entities;

public class Customer : AuditableEntity
{
    private Customer() { }

    public string Name { get; private set; } = null!;
    /// <summary>CUIT / CUIL / DNI without dashes.</summary>
    public string TaxId { get; private set; } = null!;
    public TaxCondition TaxCondition { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static Customer Create(
        string name, string taxId, TaxCondition taxCondition, string? email, string? phone, string? address, DateTime now)
    {
        var customer = new Customer();
        customer.Apply(name, taxId, taxCondition, email, phone, address);
        customer.MarkCreated(now);
        return customer;
    }

    public void Update(
        string name, string taxId, TaxCondition taxCondition, string? email, string? phone, string? address,
        bool isActive, DateTime now)
    {
        Apply(name, taxId, taxCondition, email, phone, address);
        IsActive = isActive;
        MarkUpdated(now);
    }

    private void Apply(string name, string taxId, TaxCondition taxCondition, string? email, string? phone, string? address)
    {
        Name = Guard.NotEmpty(name, "La razón social");
        TaxId = TaxIds.Normalize(taxId);
        TaxCondition = taxCondition;
        Email = email?.Trim().ToLowerInvariant();
        Phone = phone?.Trim();
        Address = address?.Trim();
    }
}

public class Supplier : AuditableEntity
{
    private Supplier() { }

    public string Name { get; private set; } = null!;
    public string TaxId { get; private set; } = null!;
    public string? ContactName { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static Supplier Create(
        string name, string taxId, string? contactName, string? email, string? phone, string? address, DateTime now)
    {
        var supplier = new Supplier();
        supplier.Apply(name, taxId, contactName, email, phone, address);
        supplier.MarkCreated(now);
        return supplier;
    }

    public void Update(
        string name, string taxId, string? contactName, string? email, string? phone, string? address,
        bool isActive, DateTime now)
    {
        Apply(name, taxId, contactName, email, phone, address);
        IsActive = isActive;
        MarkUpdated(now);
    }

    private void Apply(string name, string taxId, string? contactName, string? email, string? phone, string? address)
    {
        Name = Guard.NotEmpty(name, "La razón social");
        TaxId = TaxIds.Normalize(taxId);
        ContactName = contactName?.Trim();
        Email = email?.Trim().ToLowerInvariant();
        Phone = phone?.Trim();
        Address = address?.Trim();
    }
}

public static class TaxIds
{
    public static string Normalize(string taxId) =>
        new(Guard.NotEmpty(taxId, "El CUIT/DNI").Where(char.IsDigit).ToArray());

    /// <summary>Validates a CUIT/CUIL check digit (módulo 11).</summary>
    public static bool IsValidCuit(string? value)
    {
        if (value is null)
        {
            return false;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length != 11 || value.Any(c => !char.IsDigit(c) && c != '-'))
        {
            return false;
        }

        int[] weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        var sum = weights.Select((w, i) => w * (digits[i] - '0')).Sum();
        var check = 11 - (sum % 11);
        check = check switch { 11 => 0, 10 => 9, _ => check };
        return check == digits[10] - '0';
    }
}
