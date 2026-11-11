using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Stockly.Application.Common;
using Stockly.Application.Features.Auth;
using Stockly.Domain.Entities;
using Stockly.Domain.Enums;

namespace Stockly.Application.Features.Users;

public sealed record CreateUserRequest(string Email, string FullName, UserRole Role, string Password);

public sealed record UpdateUserRequest(string FullName, UserRole Role, bool IsActive, string? NewPassword);

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(160);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.Password).SetValidator(new PasswordValidator());
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Role).IsInEnum();
        When(x => !string.IsNullOrEmpty(x.NewPassword), () => RuleFor(x => x.NewPassword!).SetValidator(new PasswordValidator()));
    }
}

public sealed class PasswordValidator : AbstractValidator<string>
{
    public PasswordValidator()
    {
        RuleFor(p => p)
            .NotEmpty()
            .MinimumLength(10).WithMessage("La contraseña debe tener al menos 10 caracteres.")
            .Matches("[A-Za-z]").WithMessage("La contraseña debe incluir letras.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir números.");
    }
}

public sealed class UserService(IAppDbContext db, IPasswordHasher hasher, IBusinessClock clock, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct) =>
        (await db.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync(ct)).Select(AuthService.ToDto).ToList();

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new ConflictException($"Ya existe un usuario con el email {email}.");
        }

        var user = User.Create(email, request.FullName, request.Role, clock.UtcNow);
        user.SetPasswordHash(hasher.Hash(user, request.Password), clock.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return AuthService.ToDto(user);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("Usuario", id);

        if (id == currentUser.UserId && (!request.IsActive || request.Role != UserRole.Admin))
        {
            throw new ConflictException("No podés quitarte el rol de administrador ni desactivar tu propio usuario.");
        }

        user.Update(request.FullName, request.Role, request.IsActive, clock.UtcNow);
        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            user.SetPasswordHash(hasher.Hash(user, request.NewPassword), clock.UtcNow);
        }

        await db.SaveChangesAsync(ct);
        return AuthService.ToDto(user);
    }
}
