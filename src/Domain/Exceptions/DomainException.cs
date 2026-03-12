namespace TaskFlow.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public class NotFoundException : Exception
{
    public NotFoundException(string entity, object key)
        : base($"{entity} with id '{key}' was not found.") { }
}

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Unauthorized.") : base(message) { }
}
