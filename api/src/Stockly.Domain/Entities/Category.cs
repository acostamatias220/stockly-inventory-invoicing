using Stockly.Domain.Common;

namespace Stockly.Domain.Entities;

public class Category : Entity
{
    private Category() { }

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    /// <summary>Hex color used by the UI (badges and the stock-by-category donut).</summary>
    public string Color { get; private set; } = "#6366F1";

    public static Category Create(string name, string? description, string? color) =>
        new() { Name = Guard.NotEmpty(name, "El nombre"), Description = description?.Trim(), Color = color ?? "#6366F1" };

    public void Update(string name, string? description, string? color)
    {
        Name = Guard.NotEmpty(name, "El nombre");
        Description = description?.Trim();
        Color = color ?? Color;
    }
}
