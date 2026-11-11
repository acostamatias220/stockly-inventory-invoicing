using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Stockly.IntegrationTests.Infrastructure;

internal static class HttpExtensions
{
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response, System.Net.HttpStatusCode? expected = null)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (expected is not null)
        {
            response.StatusCode.Should().Be(expected.Value, body);
        }
        else
        {
            response.IsSuccessStatusCode.Should().BeTrue(body);
        }

        return JsonDocument.Parse(body).RootElement.Clone();
    }

    public static async Task<JsonElement> GetJsonAsync(this HttpClient client, string url) =>
        await (await client.GetAsync(url)).JsonAsync();

    public static async Task<JsonElement> PostJsonAsync(this HttpClient client, string url, object? body, System.Net.HttpStatusCode? expected = null) =>
        await (await client.PostAsJsonAsync(url, body, StocklyApiFactory.Json)).JsonAsync(expected);

    public static async Task<JsonElement> PutJsonAsync(this HttpClient client, string url, object body, System.Net.HttpStatusCode? expected = null) =>
        await (await client.PutAsJsonAsync(url, body, StocklyApiFactory.Json)).JsonAsync(expected);

    public static int Int(this JsonElement element, string property) => element.GetProperty(property).GetInt32();

    public static string? Str(this JsonElement element, string property) => element.GetProperty(property).GetString();

    public static decimal Dec(this JsonElement element, string property) => element.GetProperty(property).GetDecimal();
}
