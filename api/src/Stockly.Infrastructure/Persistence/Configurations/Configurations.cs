using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stockly.Domain.Entities;

namespace Stockly.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(u => u.Email).HasMaxLength(160);
        b.Property(u => u.FullName).HasMaxLength(120);
        b.Property(u => u.PasswordHash).HasMaxLength(300);
        b.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(u => u.Email).IsUnique();
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(c => c.Name).HasMaxLength(80);
        b.Property(c => c.Description).HasMaxLength(300);
        b.Property(c => c.Color).HasMaxLength(7).IsFixedLength();
        b.HasIndex(c => c.Name).IsUnique();
    }
}

internal sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> b)
    {
        b.Property(w => w.Code).HasMaxLength(10);
        b.Property(w => w.Name).HasMaxLength(80);
        b.HasIndex(w => w.Code).IsUnique();
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.Property(p => p.Sku).HasMaxLength(32);
        b.Property(p => p.Name).HasMaxLength(150);
        b.Property(p => p.Description).HasMaxLength(1000);
        b.Property(p => p.VatRate).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(p => p.Sku).IsUnique();
        b.HasIndex(p => p.Name);
        b.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(p => p.StockLevels).WithOne(s => s.Product).HasForeignKey(s => s.ProductId);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_products_price", "price >= 0");
            t.HasCheckConstraint("ck_products_minimum_stock", "minimum_stock >= 0");
        });
    }
}

internal sealed class StockLevelConfiguration : IEntityTypeConfiguration<StockLevel>
{
    public void Configure(EntityTypeBuilder<StockLevel> b)
    {
        b.HasKey(s => new { s.ProductId, s.WarehouseId });
        b.HasOne(s => s.Warehouse).WithMany().HasForeignKey(s => s.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.Property(s => s.UpdatedAt).HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        // Defense in depth: even a buggy query can't leave negative stock (MySQL ≥ 8.0.16 enforces CHECK).
        b.ToTable(t => t.HasCheckConstraint("ck_stock_levels_quantity", "quantity >= 0"));
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.Property(m => m.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(m => m.Reason).HasMaxLength(250);
        b.Property(m => m.Reference).HasMaxLength(60);
        b.HasOne(m => m.Product).WithMany().HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(m => m.Warehouse).WithMany().HasForeignKey(m => m.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(m => m.Supplier).WithMany().HasForeignKey(m => m.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Invoice>().WithMany().HasForeignKey(m => m.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(m => new { m.ProductId, m.CreatedAt });
        b.HasIndex(m => new { m.WarehouseId, m.CreatedAt });
        b.HasIndex(m => m.CorrelationId);
    }
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.Property(c => c.Name).HasMaxLength(150);
        b.Property(c => c.TaxId).HasMaxLength(11);
        b.Property(c => c.TaxCondition).HasConversion<string>().HasMaxLength(30);
        b.Property(c => c.Email).HasMaxLength(160);
        b.Property(c => c.Phone).HasMaxLength(40);
        b.HasIndex(c => c.TaxId).IsUnique();
        b.HasIndex(c => c.Name);
    }
}

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.Property(s => s.Name).HasMaxLength(150);
        b.Property(s => s.TaxId).HasMaxLength(11);
        b.Property(s => s.ContactName).HasMaxLength(120);
        b.Property(s => s.Email).HasMaxLength(160);
        b.Property(s => s.Phone).HasMaxLength(40);
        b.HasIndex(s => s.TaxId).IsUnique();
    }
}

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.Property(i => i.Letter).HasConversion<string>().HasMaxLength(1);
        b.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(i => i.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        b.Property(i => i.DiscountPercent).HasPrecision(5, 2);
        b.Property(i => i.Notes).HasMaxLength(500);
        b.Property(i => i.CancellationReason).HasMaxLength(250);
        b.Property(i => i.Version).IsConcurrencyToken();

        // Correlative numbering per letter + point of sale. NULL (drafts) is allowed many times.
        b.HasIndex(i => new { i.Letter, i.PointOfSale, i.Number }).IsUnique();
        b.HasIndex(i => new { i.Status, i.IssueDate });
        b.HasIndex(i => i.CustomerId);

        b.HasOne(i => i.Customer).WithMany().HasForeignKey(i => i.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.Warehouse).WithMany().HasForeignKey(i => i.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.CreatedBy).WithMany().HasForeignKey(i => i.CreatedById).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(i => i.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.ToTable(t => t.HasCheckConstraint("ck_invoices_discount", "discount_percent BETWEEN 0 AND 100"));
    }
}

internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> b)
    {
        b.Property(l => l.Sku).HasMaxLength(32);
        b.Property(l => l.Description).HasMaxLength(150);
        b.Property(l => l.DiscountPercent).HasPrecision(5, 2);
        b.Property(l => l.VatRate).HasConversion<string>().HasMaxLength(20);
        b.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("ck_invoice_lines_quantity", "quantity > 0"));
    }
}

internal sealed class InvoiceSequenceConfiguration : IEntityTypeConfiguration<InvoiceSequence>
{
    public void Configure(EntityTypeBuilder<InvoiceSequence> b)
    {
        b.HasKey(s => new { s.Letter, s.PointOfSale });
        b.Property(s => s.Letter).HasConversion<string>().HasMaxLength(1);
    }
}
