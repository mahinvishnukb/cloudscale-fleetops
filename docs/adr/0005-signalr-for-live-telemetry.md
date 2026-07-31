# ADR 0005 — SignalR for the live feed

**Status:** Accepted · 2026-07-30

## Context

The dashboard shows live vessel telemetry. Options: poll on an interval, Server-Sent Events, or
WebSockets via SignalR.

## Decision

SignalR, with per-vessel groups, and polling retained as a fallback.

## Rationale

Polling every fleet vessel every few seconds scales badly and is stale by design. SignalR gives
automatic transport negotiation (WebSockets, falling back to SSE and long polling), built-in
reconnection with backoff, and typed client methods.

Per-vessel groups matter: a dashboard watching one ship should not be woken by traffic from the
other 500. The vessel detail view joins `vessel:{id}`; the overview subscribes to a fleet-wide
broadcast that carries only summary fields.

## Consequences

**Auth.** Browsers cannot set headers on a WebSocket handshake, so the token travels as an
`access_token` query parameter. `JwtBearerEvents.OnMessageReceived` accepts it *only* for paths
under the hub route.

**Load balancing.** A client must keep reaching the same pod for the life of a connection. The
Service sets `sessionAffinity: ClientIP`. At real scale this would move to the Redis backplane
instead, which is a deliberate deferral, not an oversight.

**Graceful degradation.** If the socket fails to connect, the UI logs a warning and keeps the
polled data path. The live feed is an enhancement, never a hard dependency — the connection
indicator in the header shows which mode is active.
