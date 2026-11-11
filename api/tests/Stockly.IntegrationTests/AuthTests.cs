using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Stockly.IntegrationTests.Infrastructure;

namespace Stockly.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthTests(StocklyApiFactory factory)
{
    [Fact]
    public async Task Login_returns_token_and_user_with_role()
    {
        var client = factory.CreateClient();
        var login = await client.PostJsonAsync("/api/auth/login", new { email = "ADMIN@stockly.dev", password = StocklyApiFactory.DemoPassword });

        login.Str("accessToken").Should().NotBeNullOrEmpty();
        login.GetProperty("user").Str("role").Should().Be("admin");
    }

    [Theory]
    [InlineData("admin@stockly.dev", "incorrecta-123")]
    [InlineData("nadie@stockly.dev", StocklyApiFactory.DemoPassword)]
    public async Task Wrong_credentials_get_the_same_401(string email, string password)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });
        var problem = await response.JsonAsync(HttpStatusCode.Unauthorized);
        problem.Str("detail").Should().Be("Email o contraseña incorrectos.");
    }

    [Fact]
    public async Task Endpoints_require_a_token_by_default()
    {
        var response = await factory.CreateClient().GetAsync("/api/products");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_returns_the_authenticated_user()
    {
        var seller = await factory.SellerAsync();
        var me = await seller.GetJsonAsync("/api/auth/me");
        me.Str("email").Should().Be("ventas@stockly.dev");
        me.Str("role").Should().Be("vendedor");
    }

    [Theory]
    [InlineData("ventas@stockly.dev", "/api/products", HttpStatusCode.Forbidden)]   // vendedor can't manage inventory
    [InlineData("deposito@stockly.dev", "/api/invoices", HttpStatusCode.Forbidden)] // depósito can't sell
    [InlineData("deposito@stockly.dev", "/api/users", HttpStatusCode.Forbidden)]    // only admin manages users
    public async Task Roles_are_enforced_on_writes(string email, string url, HttpStatusCode expected)
    {
        var client = await factory.ClientForAsync(email);
        var response = await client.PostAsJsonAsync(url, new { });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Admin_can_create_users_with_strong_passwords_only()
    {
        var admin = await factory.AdminAsync();
        await admin.PostJsonAsync("/api/users", new { email = "debil@stockly.dev", fullName = "Débil", role = "Vendedor", password = "123" }, HttpStatusCode.BadRequest);

        var email = $"nuevo{Guid.NewGuid():N}"[..14] + "@stockly.dev";
        var user = await admin.PostJsonAsync("/api/users", new { email, fullName = "Nuevo Vendedor", role = "Vendedor", password = "Segura-2026" }, HttpStatusCode.Created);
        user.Str("role").Should().Be("vendedor");

        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = "Segura-2026" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
