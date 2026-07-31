using FleetOps.Application.Abstractions;

namespace FleetOps.UnitTests.Support;

internal sealed class FixedClock(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = utcNow;
}

internal sealed class StubCurrentUser(string username = "tester", string role = "Administrator") : ICurrentUser
{
    public string? Username => username;

    public string? Role => role;

    public bool IsInRole(string r) => string.Equals(r, role, StringComparison.Ordinal);
}
