using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Entities;

public enum UserRole { Member, Admin }

public class User : BaseEntity
{
    public int TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; } = UserRole.Member;
    public bool IsActive { get; private set; } = true;

    public Tenant Tenant { get; private set; } = null!;

    private User() { }

    public static User Create(int tenantId, string email, string fullName, string passwordHash, UserRole role = UserRole.Member)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new DomainException("Email is required.");
        if (string.IsNullOrWhiteSpace(fullName)) throw new DomainException("Full name is required.");

        return new User
        {
            TenantId = tenantId,
            Email = email.ToLowerInvariant(),
            FullName = fullName,
            PasswordHash = passwordHash,
            Role = role
        };
    }

    public void Promote() => Role = UserRole.Admin;
    public void Deactivate() => IsActive = false;
}
