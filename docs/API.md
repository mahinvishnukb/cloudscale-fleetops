# API reference

Base URL: `http://localhost:5080` locally. Interactive docs at `/swagger`.

All endpoints except `POST /api/auth/login` require `Authorization: Bearer <token>`.

## Roles

| Role | Read fleet | Manage fleet | Administer |
|---|---|---|---|
| `Analyst` | yes | — | — |
| `FleetManager` | yes | yes | — |
| `Administrator` | yes | yes | yes |

"Manage" covers registering vessels, changing status, acknowledging anomalies and uploading
manifests. "Administer" covers decommissioning.

## Endpoints

| Method | Path | Policy | Purpose |
|---|---|---|---|
| POST | `/api/auth/login` | anonymous | Exchange credentials for a JWT (rate limited: 10/min) |
| GET | `/api/auth/me` | authenticated | Inspect the caller's claims |
| GET | `/api/vessels` | ReadFleet | Paged fleet list with latest telemetry |
| GET | `/api/vessels/{id}` | ReadFleet | Single vessel |
| POST | `/api/vessels` | ManageFleet | Register a vessel |
| PATCH | `/api/vessels/{id}/status` | ManageFleet | Change status |
| DELETE | `/api/vessels/{id}` | Administer | Decommission |
| POST | `/api/vessels/{id}/telemetry` | ManageFleet | Ingest a reading; returns anomalies raised |
| GET | `/api/vessels/{id}/telemetry` | ReadFleet | Time series (default: last 6 hours) |
| GET | `/api/anomalies` | ReadFleet | Open anomalies, most severe first |
| POST | `/api/anomalies/{id}/acknowledge` | ManageFleet | Acknowledge |
| GET | `/api/analytics/fleet-health` | ReadFleet | Dashboard aggregate tiles |
| GET | `/api/manifests` | ReadFleet | Recent manifests |
| GET | `/api/manifests/{id}` | ReadFleet | Manifest with line items |
| POST | `/api/manifests/upload` | ManageFleet | Upload and ingest a CSV (max 25 MB) |
| GET | `/health/live` | anonymous | Liveness — process is up |
| GET | `/health/ready` | anonymous | Readiness — database reachable |

## SignalR hub

`/hubs/telemetry` — token passed as `?access_token=<jwt>`.

| Direction | Name | Payload |
|---|---|---|
| Server → client | `FleetTelemetryReceived` | Every reading, fleet-wide |
| Server → client | `TelemetryReceived` | Readings for a subscribed vessel |
| Server → client | `AnomalyRaised` | Newly detected anomaly |
| Client → server | `SubscribeToVessel(vesselId)` | Join a per-vessel group |
| Client → server | `UnsubscribeFromVessel(vesselId)` | Leave it |

## Errors

RFC 7807 `application/problem+json`:

```json
{
  "type": "about:blank",
  "title": "Business rule violated",
  "status": 422,
  "detail": "IMO number '9074720' failed its check-digit validation.",
  "instance": "/api/vessels",
  "correlationId": "8f3c1e2a9b7d4f60a1c5e8d2b4a70931",
  "traceId": "00-4bf92f...-01"
}
```

| Status | Meaning |
|---|---|
| 400 | Request validation failed — `errors` carries the field map |
| 401 | Missing or expired token |
| 403 | Authenticated but the role is insufficient |
| 404 | Resource not found |
| 422 | A domain invariant was violated |
| 429 | Rate limit exceeded |
| 500 | Unexpected — quote the `correlationId` |

Every response carries `X-Correlation-Id`. The same id appears in the API logs, the background
worker logs and the Lambda logs for the same operation.

## Manifest CSV format

```csv
container_number,description,gross_weight_kg,origin_port,destination_port,hazard_class
CSQU3054383,Machine parts,12000,CAVAN,NLRTM,
TGHU1234567,Industrial paint,4200,CAVAN,NLRTM,3
```

`hazard_class` is optional; the other five columns are required. Header matching ignores case
and treats spaces as underscores.

Row-level rules: ISO 6346 check digit must be valid, weight must be within 0 < w ≤ 30,480 kg,
both ports required, no duplicate container within one file. Failing rows are reported with
their line number; the remaining rows are still accepted.

For S3-triggered ingestion the object key must be `incoming/{IMO}/{VOYAGE}.csv`, e.g.
`incoming/9074729/V-2026-014.csv`.
