using FleetOps.Application.Abstractions;

namespace FleetOps.Infrastructure.Services;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
