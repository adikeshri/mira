using MediatR;

namespace Mira.Application;

public sealed class ValidationException(string message) : Exception(message);

// Queries that can be malformed implement this; the pipeline rejects them before the handler runs.
public interface IValidated
{
    string? Validate();
}

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct) =>
        request is IValidated v && v.Validate() is { } error ? throw new ValidationException(error) : next();
}
