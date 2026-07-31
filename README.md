# CloudScale FleetOps

[![CI](https://github.com/mahinvishnukb/cloudscale-fleetops/actions/workflows/ci.yml/badge.svg)](https://github.com/mahinvishnukb/cloudscale-fleetops/actions/workflows/ci.yml)

Maritime fleet operations platform: live vessel telemetry, rule-based anomaly detection, and
event-driven cargo-manifest ingestion.

**Angular 22** dashboard · **.NET 8** clean-architecture API · **AWS Lambda + S3** pipeline ·
**Terraform** · **Kubernetes** · **GitHub Actions**

> The entire stack runs locally at zero cost. LocalStack stands in for AWS, k3d for EKS, and
> Postgres in Docker for RDS. The Terraform and Kubernetes code is written for real AWS and
> validated in CI — see [Local vs. AWS parity](#local-vs-aws-parity) for exactly what that does
> and does not prove.

---

## Contents

- [What it does](#what-it-does)
- [Architecture](#architecture)
- [Running it locally](#running-it-locally)
- [Repository layout](#repository-layout)
- [Design decisions worth explaining](#design-decisions-worth-explaining)
- [Testing](#testing)
- [Deployment](#deployment)
- [Local vs. AWS parity](#local-vs-aws-parity)

---

## What it does

| Capability | Detail |
|---|---|
| **Fleet register** | Vessels validated by IMO check digit — an invalid IMO cannot exist in the domain |
| **Live telemetry** | Position, speed, RPM, fuel flow and engine temperature streamed over SignalR |
| **Real AIS (optional)** | Ingests live vessel traffic from aisstream.io; falls back to a simulator with no API key |
| **Anomaly detection** | Five rules: overheat, fuel spike, implausible speed, GPS jump (haversine), sensor dropout |
| **Manifest ingestion** | CSV manifests validated row-by-row; ISO 6346 container check digits, weight limits, duplicate detection |
| **Event-driven pipeline** | S3 upload → Lambda → Postgres, so a 40 MB manifest never occupies an API worker |
| **RBAC** | JWT with three roles: Administrator, FleetManager, Analyst |
| **Observability** | Structured JSON logs (Serilog) with a correlation id threaded across API, worker and Lambda |

### Where the data comes from

The dashboard runs on one of two telemetry sources, chosen by configuration:

| Mode | When | Data |
|---|---|---|
| **Simulator** (default) | no AIS API key | Entirely synthetic — six fictional vessels |
| **Live AIS** | `Ais__Enabled=true` + a free API key | Real vessel traffic from [aisstream.io](https://aisstream.io) |

In AIS mode, position, speed, course, heading, navigational status, vessel name and IMO number
are **real**. Engine temperature, RPM and fuel flow are **derived from speed** — AIS does not
broadcast them, and no public source provides them. Gross tonnage is estimated from broadcast
dimensions. This is stated plainly in the UI and in
[ADR 0006](docs/adr/0006-live-ais-ingestion.md); nothing here is presented as measured when it
is not.

---

## Architecture

```
                    ┌───────────────────────────────────────────┐
                    │  Angular 22 SPA (standalone, zoneless)     │
                    │   Tailwind · Chart.js · SignalR client     │
                    │              [Vercel]                      │
                    └───────────────────┬───────────────────────┘
                                        │ HTTPS / WSS
                                        │
                    ┌───────────────────▼───────────────────────┐
                    │  API Gateway (AWS)  ·  Ingress (k8s)       │
                    │  CORS · rate limiting · request validation │
                    └───────────────────┬───────────────────────┘
                                        │
             ┌──────────────────────────┴──────────────────────────┐
             │                                                     │
   ┌─────────▼──────────────┐                        ┌─────────────▼─────────────┐
   │  .NET 8 Web API        │                        │  Manifest Ingestion       │
   │  Clean Architecture    │                        │  Lambda (.NET 8)          │
   │                        │                        │                           │
   │  Api ──────────────┐   │                        │  S3 ObjectCreated trigger │
   │  Infrastructure    │   │                        │  incoming/{IMO}/{VOY}.csv │
   │  Application       │   │                        │  → processed/ | rejected/ │
   │  Domain  ◄─────────┘   │                        │  DLQ on failure           │
   │                        │                        │                           │
   │  · JWT + RBAC          │                        │  Shares the SAME          │
   │  · SignalR hub         │                        │  ManifestIngestionService │
   │  · Anomaly detector    │◄───────────────────────┤  as the API, so rules     │
   │  · Background worker   │                        │  cannot drift             │
   └──────────┬─────────────┘                        └─────────────┬─────────────┘
              │                                                    │
              └───────────────────┬────────────────────────────────┘
                                  │
         ┌────────────────────────┼────────────────────────┐
         │                        │                        │
   ┌─────▼──────┐         ┌───────▼───────┐        ┌───────▼────────┐
   │ PostgreSQL │         │  S3 (objects) │        │  CloudWatch    │
   │ RDS / Neon │         │  LocalStack   │        │  structured    │
   │            │         │  locally      │        │  JSON logs     │
   └────────────┘         └───────────────┘        └────────────────┘
```

The dependency rule points inward: `Api → Infrastructure → Application → Domain`.
`FleetOps.Domain` has **zero** package references — it is enforced by having nothing to
reference in its `.csproj`.

---

## Running it locally

**Prerequisites:** Docker, .NET 8 SDK, Node 22.12+ or 24. All free.
(`frontend/.nvmrc` pins the version; Angular 22 requires TypeScript 6.)

```bash
git clone <your-repo-url> fleetops && cd fleetops

# 1. Configure secrets (nothing real — this is all local)
cp .env.example .env
echo "POSTGRES_PASSWORD=localdev"                >> .env
echo "Jwt__Key=$(openssl rand -base64 48)"       >> .env
echo "DEMO_PASSWORD=fleetops-demo-2026"          >> .env

# 2. Start Postgres + LocalStack + the API
make up

# 3. Start the dashboard
make ui-install
make ui
```

| Service | URL |
|---|---|
| Dashboard | http://localhost:4200 |
| API | http://localhost:5080 |
| Swagger | http://localhost:5080/swagger |
| LocalStack | http://localhost:4566 |

Sign in as `admin`, `manager` or `analyst` with the `DEMO_PASSWORD` you set above.
The telemetry simulator starts automatically, so the dashboard is populated within seconds
and raises an anomaly roughly every two minutes.

### The event-driven pipeline, end to end

```bash
make lambda        # build the Lambda zip
make tf-apply      # provision S3 + Lambda + IAM + CloudWatch in LocalStack
make manifest-demo # drop a manifest into the bucket and watch it get processed
```

`sample-manifest.csv` deliberately contains one invalid container number and one duplicate,
so the run demonstrates partial acceptance rather than an all-or-nothing failure.

### On real Kubernetes

```bash
make k8s-up   # creates a 3-node k3d cluster and deploys into it
```

This is genuine Kubernetes (k3s in Docker), so the Deployment, Service, Ingress, probes,
rolling update and PDB are all actually exercised.

---

## Repository layout

```
backend/
  src/
    FleetOps.Domain/          entities, value objects, invariants — no dependencies
    FleetOps.Application/     use cases, DTOs, ports, anomaly rules, CSV parser
    FleetOps.Infrastructure/  EF Core, Postgres, JWT, PBKDF2, S3
    FleetOps.Api/             controllers, middleware, SignalR hub, background worker
  tests/FleetOps.UnitTests/   xUnit — domain, rules, parser, ingestion
lambda/src/FleetOps.ManifestProcessor/   S3-triggered ingestion function
frontend/                     Angular 22 standalone, signals, zoneless
infra/terraform/              S3, Lambda, IAM, CloudWatch, SQS DLQ
k8s/base + k8s/overlays/local Deployment, Service, Ingress, HPA, PDB, k3d config
.github/workflows/            CI (build, test, validate, image) and manual deploy
scripts/                      lambda build, EF migration, k3d bring-up
```

---

## Design decisions worth explaining

Full write-ups in [`docs/adr/`](docs/adr). The short version:

**IMO and container numbers are value objects, not strings.**
Both carry check digits. Validating at the boundary means an invalid identifier cannot reach
the database. `ImoNumber.Create()` either returns a valid number or throws.

**The anomaly detector is a pure function.**
`Evaluate(current, previous, now)` takes no clock, no database and no DI — which is why there
are twelve tests covering the rules, including the boundary cases and the divide-by-zero that
fuel-per-mile invites at zero speed.

**The Lambda and the API share one ingestion service.**
Two entry points, one set of validation rules. The alternative — a copy of the parser in the
Lambda — is how the two paths silently diverge six months later.

**Manifests are partially accepted.**
One malformed weight should not strand the other 4,000 containers on the ship. Good rows are
persisted, bad rows are recorded as errors, and the manifest lands in `AcceptedWithWarnings`.

**No CPU limit on the API pod.**
CFS throttling costs more p99 latency than the noisy-neighbour risk it removes. Memory is
still capped, and requests are set so the scheduler can do its job.

**Real AIS data validates the domain model, rather than the other way round.**
Live AIS is full of transponders broadcasting zeroed IMO numbers, aids to navigation using
ship-like MMSIs, and in-band sentinels (speed 102.3, position 91/181, heading 511) meaning
"not available". `ImoNumber`, `MmsiNumber` and `AisSentinels` reject all of it at the
boundary. The check-digit validation that looked like over-engineering against seed data
earns its keep the moment real traffic arrives.

**Hand-rolled RFC 4180 CSV reader.**
Quoted commas, doubled quotes and embedded newlines are the three things naive `Split(',')`
gets wrong, and manifests contain all three. It is ~90 lines and has nine tests.

**The frontend is zoneless.**
Angular 22 ships without zone.js, so change detection is driven entirely by signals. That is
why component state is held in `signal()` and `computed()` rather than plain fields — including
the form inputs, which use `[ngModel]`/`(ngModelChange)` against signals instead of `[(ngModel)]`
on a field the framework would no longer notice changing.

**MediatR and AutoMapper are deliberately absent.**
Both moved to commercial licensing. Plain application services and explicit mapping cost a
few more lines and remove a licensing question from the project entirely.

---

## Testing

```bash
make test
```

| Area | What is covered |
|---|---|
| Domain | IMO and ISO 6346 check digits, tonnage and weight limits, status transitions, manifest state machine |
| Rules | All five anomaly rules, threshold boundaries, configurable thresholds, the stationary divide-by-zero |
| Parsing | RFC 4180 quoting, CRLF, embedded newlines, duplicate detection, per-row error line numbers |
| Ingestion | Accept / warn / reject outcomes against a real relational database |

Persistence tests run against **SQLite in memory**, not the EF in-memory provider — the latter
ignores relational semantics, which is exactly what needs testing. One test specifically guards
the `ValueComparer` on the validation-errors list, because without it EF silently skips the update.

---

## Deployment

See [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md) for the full walkthrough. Summary:

| Component | Host | Cost |
|---|---|---|
| Angular dashboard | Vercel Hobby | Free |
| .NET API | Render (Docker, free plan) | Free — sleeps after 15 min idle |
| Postgres | Neon free tier | Free — permanent, no card |
| S3 + Lambda | LocalStack, local demo only | Free |

The login screen tells the user when the API is waking from sleep, so a cold link reads as a
free-tier tradeoff rather than a broken app.

---

## Local vs. AWS parity

Being straight about this matters more than pretending otherwise.

**Genuinely exercised locally:** Kubernetes Deployment/Service/Ingress/probes/rolling updates
(k3d runs real k3s), S3 API semantics, Lambda invocation and the S3 event contract, IAM policy
documents, Terraform plan/apply against LocalStack, the full request path through the API.

**Not exercised locally:** IRSA pod identity, the AWS Load Balancer Controller, EBS CSI volumes,
RDS failover, real API Gateway, CloudWatch alarm delivery. Those resources are written in
Terraform and validated in CI, but they have never been applied to a live account.

If you want to run it on real AWS, set `use_localstack = false` in `infra/terraform` — but read
`docs/DEPLOYMENT.md` first, because EKS alone is roughly **US$73/month** before a single node.

---

## Licence

MIT — see [LICENSE](LICENSE).
