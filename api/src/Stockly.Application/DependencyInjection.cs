using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Stockly.Application.Features.Auth;
using Stockly.Application.Features.Catalog;
using Stockly.Application.Features.Dashboard;
using Stockly.Application.Features.Invoices;
using Stockly.Application.Features.Parties;
using Stockly.Application.Features.Products;
using Stockly.Application.Features.Stock;
using Stockly.Application.Features.Users;

namespace Stockly.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Built-in FluentValidation messages in Spanish (custom ones are already written in Spanish).
        ValidatorOptions.Global.LanguageManager.Culture = new System.Globalization.CultureInfo("es");
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>(includeInternalTypes: true);

        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<ProductService>();
        services.AddScoped<StockService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<DashboardService>();
        return services;
    }
}
