namespace FleetOps.Domain.Common;

/// <summary>
/// Thrown when an operation would leave an aggregate in an invalid state.
/// Surfaces as HTTP 422 via the global exception middleware.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
