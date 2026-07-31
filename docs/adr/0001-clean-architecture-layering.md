# ADR 0001 — Clean Architecture layering

**Status:** Accepted · 2026-07-30

## Context

The API needs to serve HTTP, host a SignalR hub, run a background worker, and share its
ingestion logic with an AWS Lambda. Business rules that live in controllers cannot be reused
by a Lambda, and rules that live in EF entities cannot be tested without a database.

## Decision

Four projects with an inward dependency rule:

```
FleetOps.Api → FleetOps.Infrastructure → FleetOps.Application → FleetOps.Domain
```

- **Domain** — entities, value objects, invariants. Its `.csproj` has no `PackageReference`
  at all, so the rule is enforced by the build, not by discipline.
- **Application** — use cases, DTOs, and the ports (`IFleetOpsDbContext`, `IManifestStorage`,
  `ITelemetryBroadcaster`, `IDateTimeProvider`) that outer layers implement.
- **Infrastructure** — EF Core, Npgsql, JWT, PBKDF2, S3.
- **Api** — controllers, middleware, hub, hosted service.

## Consequences

Good: the Lambda references Application and Infrastructure and gets the entire ingestion
pipeline for free. The anomaly rules and CSV parser are testable with no database and no host.

Cost: more projects and more indirection than a single-project API. For a CRUD app that would
be overhead; here, two independent entry points share one rule set, which is exactly the case
this layering pays for.
