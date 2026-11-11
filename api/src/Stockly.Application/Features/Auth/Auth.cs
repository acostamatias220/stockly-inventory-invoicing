using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Stockly.Application.Common;
using Stockly.Domain.Entities;

namespace Stockly.Application.Features.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record UserDto(int Id, string Email, string FullName, string Role, bool IsActive, DateTime? LastLoginAt);

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAt, UserDto User);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class AuthService(IAppDbContext db, IPasswordHasher hasher, ITokenService tokens, IBusinessClock clock)
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

        // Same message for unknown user / wrong password / disabled user: don't leak which emails exist.
        if (user is null || !user.IsActive || !hasher.Verify(user, request.Password))
        {
            throw new UnauthorizedException("Email o contraseña incorrectos.");
        }

        user.RegisterLogin(clock.UtcNow);
        await db.SaveChangesAsync(ct);

        var (token, expiresAt) = tokens.CreateAccessToken(user);
        return new LoginResponse(token, expiresAt, ToDto(user));
    }

    public async Task<UserDto> MeAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("Usuario", userId);
        return ToDto(user);
    }

    internal static UserDto ToDto(User u) =>
        new(u.Id, u.Email, u.FullName, Roles.From(u.Role), u.IsActive, u.LastLoginAt);
}
