using Microsoft.Extensions.Configuration;
using Stockly.Application.Common;

namespace Stockly.Infrastructure.Time;

/// <summary>Stores UTC, but derives business dates (issue date, "today", monthly KPIs) in the company time zone.</summary>
internal sealed class BusinessClock : IBusinessClock
{
    private readonly TimeProvider _time;
    private readonly TimeZoneInfo _zone;

    public BusinessClock(TimeProvider time, IConfiguration configuration)
    {
        _time = time;
        var zoneId = configuration["App:TimeZone"] ?? "America/Argentina/Buenos_Aires";
        _zone = TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var zone)
            ? zone
            : TimeZoneInfo.CreateCustomTimeZone("ART", TimeSpan.FromHours(-3), "Argentina", "Argentina");
    }

    public DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    public DateOnly Today => ToBusinessDate(UtcNow);

    public DateTime StartOfDayUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), _zone);

    public DateOnly ToBusinessDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone));
}
