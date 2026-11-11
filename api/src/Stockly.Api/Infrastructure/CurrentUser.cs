using System.Globalization;
using Stockly.Application.Common;
using Stockly.Domain.Enums;

namespace Stockly.Api.Infrastructure;

/// <summary>Reads the authenticated user from the JWT claims of the current request.</summary>
internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public int UserId => int.Parse(
        Principal.FindFirst("sub")?.Value ?? throw new UnauthorizedException("Sesión inválida."), CultureInfo.InvariantCulture);

    public UserRole Role => Principal.FindFirst("role")?.Value switch
    {
        Roles.Admin => UserRole.Admin,
        Roles.Vendedor => UserRole.Vendedor,
        Roles.Deposito => UserRole.Deposito,
        _ => throw new UnauthorizedException("Sesión inválida."),
    };

    private System.Security.Claims.ClaimsPrincipal Principal =>
        accessor.HttpContext?.User ?? throw new UnauthorizedException("No hay un usuario autenticado.");
}
