using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Stockly.Application.Common;
using Stockly.Application.Features.Auth;

namespace Stockly.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(AuthService auth, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Exchanges email + password for a JWT access token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<LoginResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    /// <summary>Returns the authenticated user.</summary>
    [HttpGet("me")]
    public Task<UserDto> Me(CancellationToken ct) => auth.MeAsync(currentUser.UserId, ct);
}
