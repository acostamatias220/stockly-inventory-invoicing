using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stockly.Application.Common;
using Stockly.Application.Features.Invoices;
using Stockly.Infrastructure.Pdf;
using Stockly.Infrastructure.Persistence;
using Stockly.Infrastructure.Security;
using Stockly.Infrastructure.Stock;
using Stockly.Infrastructure.Time;

namespace Stockly.Infrastructure;

public static class DependencyInjection
{
    /// <summary>MySQL version targeted by the migrations. Fixed (instead of AutoDetect) so startup doesn't need a round-trip.</summary>
    public static readonly MySqlServerVersion ServerVersion = new(new Version(8, 4, 0));

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Falta ConnectionStrings:Default. Configuralo con user-secrets o con la variable ConnectionStrings__Default.");
        }

        services.AddDbContext<AppDbContext>(options => options
            .UseMySql(connectionString, ServerVersion, mysql => mysql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IStockStore, MySqlStockStore>();
        services.AddScoped<IInvoiceNumberGenerator, MySqlInvoiceNumberGenerator>();
        services.AddScoped<DbSeeder>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IBusinessClock, BusinessClock>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IInvoicePdfRenderer, QuestPdfInvoiceRenderer>();

        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.Section))
            .Validate(o => System.Text.Encoding.UTF8.GetByteCount(o.Key) >= 32, "Jwt:Key debe tener al menos 32 bytes (256 bits).")
            .ValidateOnStart();
        services.Configure<CompanyOptions>(configuration.GetSection(CompanyOptions.Section));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.Section));
        services.Configure<InvoicingOptions>(configuration.GetSection(InvoicingOptions.Section));

        return services;
    }
}
