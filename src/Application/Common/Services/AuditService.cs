using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Common.Services;

public class AuditService : IAuditService
{
    private readonly IAuditRepository _repository;
    private readonly ICurrentUserService _currentUser;

    public AuditService(IAuditRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task LogAsync(string action, string entityName, int? entityId = null,
        string? oldValues = null, string? newValues = null)
    {
        var user = _currentUser.User;
        var log = AuditLog.Create(
            tenantId: user?.TenantId ?? 0,
            userId: user?.Id,
            userEmail: user?.Email ?? "system",
            action: action,
            entityName: entityName,
            entityId: entityId,
            oldValues: oldValues,
            newValues: newValues
        );
        await _repository.AddAsync(log);
    }
}
