using System.Globalization;
using System.Text;

namespace Stockly.Application.Common;

/// <summary>Minimal RFC 4180 CSV writer. Output is UTF-8 with BOM so Excel opens accents correctly.</summary>
public static class Csv
{
    public static byte[] Write<T>(IEnumerable<T> rows, params (string Header, Func<T, object?> Value)[] columns)
    {
        var sb = new StringBuilder();
        sb.AppendJoin(',', columns.Select(c => Escape(c.Header))).Append("\r\n");
        foreach (var row in rows)
        {
            sb.AppendJoin(',', columns.Select(c => Escape(Format(c.Value(row))))).Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    internal static string Format(object? value) => value switch
    {
        null => string.Empty,
        decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    internal static string Escape(string value)
    {
        // Neutralize spreadsheet formula injection (=, +, -, @ at the start of a text cell).
        if (value.Length > 0 && "=+@".Contains(value[0], StringComparison.Ordinal))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }
}
