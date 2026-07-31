# ADR 0003 — Manifests are partially accepted

**Status:** Accepted · 2026-07-30

## Context

A cargo manifest can carry thousands of container rows. Rows arrive malformed regularly: a
mistyped container number, a weight with a thousands separator, a missing destination port.

The naive choice is to reject the whole file on the first bad row.

## Decision

Row-level validation with partial acceptance. Valid rows are persisted; invalid rows are
recorded as structured errors carrying the file line number and column. The manifest resolves to:

| Rows persisted | Errors | Status |
|---|---|---|
| 0 | any | `Rejected` |
| ≥1 | 0 | `Accepted` |
| ≥1 | ≥1 | `AcceptedWithWarnings` |

## Rationale

Rejecting 4,000 good containers because one weight has a stray comma stops a ship. Operations
staff need the valid cargo to flow and a precise list of what to fix — line number and column,
not "parse error".

## Consequences

`CargoManifest` owns the status calculation, so both the API and the Lambda reach the same
verdict from the same code. Callers must handle three success shapes rather than two, and the
UI has to surface warnings — the manifests table renders the error list in a collapsible cell.
