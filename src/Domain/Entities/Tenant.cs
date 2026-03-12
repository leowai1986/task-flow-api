using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty; // unique identifier e.g. "acme-corp"
    public bool IsActive { get; private set; } = true;

    private readonly List<User> _users = new();
    public IReadOnlyCollection<User> Users => _users.AsReadOnly();

    private Tenant() { }

    public static Tenant Create(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Tenant name is required.");
        if (string.IsNullOrWhiteSpace(slug)) throw new DomainException("Tenant slug is required.");

        return new Tenant { Name = name, Slug = slug.ToLowerInvariant() };
    }

    public void Deactivate() => IsActive = false;
}
