using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MySqlConnector;
using Testcontainers.MySql;

namespace Stockly.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API (migrations + demo seed) against a real MySQL:
/// <list type="bullet">
/// <item>If <c>TEST_MYSQL_CONNECTION</c> is set (CI service container or a local server), a throwaway
/// database <c>stockly_it_xxxx</c> is created on that server and dropped at the end.</item>
/// <item>Otherwise a <c>mysql:8.4</c> container is started with Testcontainers (requires Docker).</item>
/// </list>
/// </summary>
public sealed class StocklyApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DemoPassword = "Integration-Test-2026";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private MySqlContainer? _container;
    private string _connectionString = string.Empty;
    private string? _serverConnection;
    private string? _database;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("TEST_MYSQL_CONNECTION");
        if (!string.IsNullOrWhiteSpace(external))
        {
            _database = $"stockly_it_{Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant()}";
            var builder = new MySqlConnectionStringBuilder(external) { Database = string.Empty };
            _serverConnection = builder.ConnectionString;
            await ExecuteAsync(_serverConnection, $"CREATE DATABASE `{_database}`");
            builder.Database = _database;
            _connectionString = builder.ConnectionString;
        }
        else
        {
            _container = new MySqlBuilder("mysql:8.4").Build();
            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        // Force host creation (runs migrations + seed once for the whole collection).
        _ = Server;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        if (_serverConnection is not null && _database is not null)
        {
            await ExecuteAsync(_serverConnection, $"DROP DATABASE IF EXISTS `{_database}`");
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Jwt:Key", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Seed:DemoPassword", DemoPassword);
        builder.UseSetting("RateLimiting:LoginPermitLimit", "1000");
    }

    public async Task<HttpClient> ClientForAsync(string email)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = DemoPassword });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
        return client;
    }

    public Task<HttpClient> AdminAsync() => ClientForAsync("admin@stockly.dev");

    public Task<HttpClient> SellerAsync() => ClientForAsync("ventas@stockly.dev");

    public Task<HttpClient> WarehouseAsync() => ClientForAsync("deposito@stockly.dev");

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<StocklyApiFactory>
{
    public const string Name = "api";
}
