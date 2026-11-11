using Stockly.Domain.Common;
using Stockly.Domain.Enums;

namespace Stockly.Domain.Entities;

public class User : AuditableEntity
{
    private User() { }

    public string Email { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? LastLoginAt { get; private set; }

    public static User Create(string email, string fullName, UserRole role, DateTime now)
    {
        var user = new User
        {
            Email = NormalizeEmail(email),
            FullName = Guard.NotEmpty(fullName, "El nombre"),
            Role = role,
        };
        user.MarkCreated(now);
        return user;
    }

    public static string NormalizeEmail(string email) => Guard.NotEmpty(email, "El email").ToLowerInvariant();

    public void SetPasswordHash(string hash, DateTime now)
    {
        PasswordHash = Guard.NotEmpty(hash, "El hash de contraseña");
        MarkUpdated(now);
    }

    public void Update(string fullName, UserRole role, bool isActive, DateTime now)
    {
        FullName = Guard.NotEmpty(fullName, "El nombre");
        Role = role;
        IsActive = isActive;
        MarkUpdated(now);
    }

    public void RegisterLogin(DateTime now) => LastLoginAt = now;
}
