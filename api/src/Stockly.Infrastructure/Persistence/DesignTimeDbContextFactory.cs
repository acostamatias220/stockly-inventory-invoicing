using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Stockly.Infrastructure.Persistence;

/// <summary>Used only by <c>dotnet ef</c> to build the model; it never opens a connection when adding migrations.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=localhost;Port=3306;Database=stockly;User=stockly";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(connectionString, DependencyInjection.ServerVersion)
            .Options;
        return new AppDbContext(options);
    }
}
