namespace Stockly.Domain.Common;

public static class Money
{
    /// <summary>Rounds to cents using the commercial rule (0.005 → 0.01).</summary>
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Applies a percentage (0–100) to an amount and rounds the result to cents.</summary>
    public static decimal Percent(decimal amount, decimal percent) => Round(amount * percent / 100m);
}
