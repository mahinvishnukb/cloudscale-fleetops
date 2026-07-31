namespace FleetOps.Application.Common;

/// <summary>Requested resource does not exist. Maps to HTTP 404.</summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.");

/// <summary>Caller is authenticated but not permitted. Maps to HTTP 403.</summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>Request failed input validation. Maps to HTTP 400 with a field-keyed payload.</summary>
public sealed class ValidationFailedException(IDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
