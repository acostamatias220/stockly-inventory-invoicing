namespace Stockly.Domain.Common;

internal static class Guard
{
    public static string NotEmpty(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("validation.required", $"{field} es obligatorio.");
        }

        return value.Trim();
    }

    public static decimal NotNegative(decimal value, string field)
    {
        if (value < 0)
        {
            throw new DomainException("validation.negative", $"{field} no puede ser negativo.");
        }

        return value;
    }

    public static decimal Percentage(decimal value, string field)
    {
        if (value is < 0 or > 100)
        {
            throw new DomainException("validation.percentage", $"{field} debe estar entre 0 y 100.");
        }

        return value;
    }

    public static int Positive(int value, string field)
    {
        if (value <= 0)
        {
            throw new DomainException("validation.positive", $"{field} debe ser mayor a cero.");
        }

        return value;
    }
}
