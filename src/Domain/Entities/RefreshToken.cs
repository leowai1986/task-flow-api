namespace TaskFlow.Domain.Entities;

public class RefreshToken
{
    public int Id { get; private set; }
    public int UserId { get; private set; }
    public int TenantId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByToken { get; private set; }

    public User User { get; private set; } = null!;

    private RefreshToken() { }

    public static RefreshToken Create(int userId, int tenantId, string token, int expiryDays = 30)
    {
        return new RefreshToken
        {
            UserId = userId,
            TenantId = tenantId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;

    public void Revoke(string? replacedByToken = null)
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
        ReplacedByToken = replacedByToken;
    }
}
