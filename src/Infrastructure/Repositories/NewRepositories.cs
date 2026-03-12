using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Interfaces;
using TaskFlow.Infrastructure.Data;

namespace TaskFlow.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _db;
    public RefreshTokenRepository(AppDbContext db) => _db = db;

    public async Task<RefreshToken?> GetByTokenAsync(string token) =>
        await _db.RefreshTokens.Include(r => r.User).FirstOrDefaultAsync(r => r.Token == token);

    public async Task<RefreshToken> AddAsync(RefreshToken token)
    {
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync();
        return token;
    }

    public async Task UpdateAsync(RefreshToken token)
    {
        _db.RefreshTokens.Update(token);
        await _db.SaveChangesAsync();
    }

    public async Task RevokeAllForUserAsync(int userId, int tenantId)
    {
        var tokens = await _db.RefreshTokens
            .Where(r => r.UserId == userId && r.TenantId == tenantId && !r.IsRevoked)
            .ToListAsync();
        foreach (var t in tokens) t.Revoke();
        await _db.SaveChangesAsync();
    }
}

public class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _db;
    public AuditRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(AuditLog log)
    {
        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync();
    }
}

public class OutboxRepository : IOutboxRepository
{
    private readonly AppDbContext _db;
    public OutboxRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(OutboxMessage message)
    {
        _db.OutboxMessages.Add(message);
        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<OutboxMessage>> GetUnprocessedAsync(int batchSize = 20) =>
        await _db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < 3)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync();

    public async Task UpdateAsync(OutboxMessage message)
    {
        _db.OutboxMessages.Update(message);
        await _db.SaveChangesAsync();
    }
}
