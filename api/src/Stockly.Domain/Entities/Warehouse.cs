using Stockly.Domain.Common;

namespace Stockly.Domain.Entities;

public class Warehouse : Entity
{
    private Warehouse() { }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Address { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static Warehouse Create(string code, string name, string? address) => new()
    {
        Code = Guard.NotEmpty(code, "El código").ToUpperInvariant(),
        Name = Guard.NotEmpty(name, "El nombre"),
        Address = address?.Trim(),
    };

    public void Update(string name, string? address, bool isActive)
    {
        Name = Guard.NotEmpty(name, "El nombre");
        Address = address?.Trim();
        IsActive = isActive;
    }
}
