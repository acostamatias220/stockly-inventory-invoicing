namespace Stockly.Domain.Common;

/// <summary>Base type for aggregate roots and entities with a surrogate integer key.</summary>
public abstract class Entity
{
    public int Id { get; protected set; }
}

/// <summary>Entities that keep creation / last update timestamps (UTC).</summary>
public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAt { get; protected set; }
    public DateTime UpdatedAt { get; protected set; }

    protected void MarkCreated(DateTime now)
    {
        CreatedAt = now;
        UpdatedAt = now;
    }

    protected void MarkUpdated(DateTime now) => UpdatedAt = now;
}
