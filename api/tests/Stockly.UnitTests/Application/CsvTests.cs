using System.Text;
using FluentAssertions;
using Stockly.Application.Common;

namespace Stockly.UnitTests.Application;

public class CsvTests
{
    private sealed record Row(string Name, decimal Price, DateOnly Date);

    [Fact]
    public void Writes_header_rows_and_utf8_bom_with_invariant_numbers()
    {
        var bytes = Csv.Write([new Row("Teclado", 18500.5m, new DateOnly(2026, 11, 10))],
            ("Producto", r => r.Name), ("Precio", r => r.Price), ("Fecha", r => r.Date));

        bytes.Take(3).Should().Equal(Encoding.UTF8.GetPreamble());
        Encoding.UTF8.GetString(bytes[3..]).Should().Be("Producto,Precio,Fecha\r\nTeclado,18500.50,2026-11-10\r\n");
    }

    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("con, coma", "\"con, coma\"")]
    [InlineData("comillas \"dobles\"", "\"comillas \"\"dobles\"\"\"")]
    [InlineData("multi\nlínea", "\"multi\nlínea\"")]
    public void Escapes_values_per_rfc4180(string value, string expected) => Csv.Escape(value).Should().Be(expected);

    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")")]
    [InlineData("+54 11 1234")]
    [InlineData("@SUM(A1)")]
    public void Neutralizes_spreadsheet_formula_injection(string value) => Csv.Escape(value).TrimStart('"').Should().StartWith("'");
}
