using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Stockly.Application.Common;
using Stockly.Domain.Entities;

namespace Stockly.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "stockly-api";
    public string Audience { get; set; } = "stockly-client";

    /// <summary>HMAC-SHA256 signing key. Never committed: user-secrets in dev, env var (Jwt__Key) elsewhere.</summary>
    public string Key { get; set; } = string.Empty;

    public int ExpiresMinutes { get; set; } = 480;

    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options, IBusinessClock clock) : ITokenService
{
    public (string Token, DateTime ExpiresAt) CreateAccessToken(User user)
    {
        var settings = options.Value;
        var now = clock.UtcNow;
        var expires = now.AddMinutes(settings.ExpiresMinutes);

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("role", Roles.From(user.Role)),
        ];

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(settings.SigningKey(), SecurityAlgorithms.HmacSha256),
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}

/// <summary>Adapter over ASP.NET Core Identity's PBKDF2 hasher (salted, versioned, upgradeable).</summary>
internal sealed class IdentityPasswordHasher : Application.Common.IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(User user, string password) =>
        !string.IsNullOrEmpty(user.PasswordHash) &&
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
