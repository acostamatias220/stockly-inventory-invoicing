namespace Stockly.Application.Common;

/// <summary>Mapped to HTTP 404.</summary>
public sealed class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' no existe.");

/// <summary>Mapped to HTTP 409 (duplicates, stale versions…).</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>Mapped to HTTP 401.</summary>
public sealed class UnauthorizedException(string message) : Exception(message);

/// <summary>Mapped to HTTP 403: authenticated, but the role can't perform this specific action.</summary>
public sealed class ForbiddenException(string message) : Exception(message);
