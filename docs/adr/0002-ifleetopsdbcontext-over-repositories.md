# ADR 0002 — `IFleetOpsDbContext` instead of per-aggregate repositories

**Status:** Accepted · 2026-07-30

## Context

The Application layer needs data access without depending on a concrete `DbContext`. The two
common options are a repository interface per aggregate, or a single context interface
exposing `DbSet<T>`.

## Decision

A single `IFleetOpsDbContext` exposing `DbSet<T>` properties plus `SaveChangesAsync`.
This means `FleetOps.Application` takes a `PackageReference` on `Microsoft.EntityFrameworkCore`.

## Rationale

EF Core's `DbSet<T>` is already a repository and `DbContext` is already a unit of work. Wrapping
them produces interfaces that either leak `IQueryable` — reproducing the coupling they were meant
to remove — or force every query shape into a named method and lose composable filtering,
paging and projection.

The dependency is on EF Core's *abstractions*, not on Npgsql. The provider is still chosen
entirely in Infrastructure, which is the coupling that actually matters.

## Consequences

Application code reads as ordinary LINQ and is testable against SQLite in memory. The tradeoff
is a package reference in a layer that purists keep empty. That is a real cost, accepted
knowingly: the alternative is a repository layer that adds indirection without adding isolation.

If the persistence technology ever changes, `IFleetOpsDbContext` is the single seam to replace.
