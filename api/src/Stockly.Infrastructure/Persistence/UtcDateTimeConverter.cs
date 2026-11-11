using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Stockly.Infrastructure.Persistence;

/// <summary>Everything is stored in UTC; this marks values read from MySQL as UTC so the JSON carries the "Z".</summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
