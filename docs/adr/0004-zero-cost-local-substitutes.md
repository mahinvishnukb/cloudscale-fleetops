# ADR 0004 — Local substitutes for every AWS service

**Status:** Accepted · 2026-07-30

## Context

The reference architecture calls for EKS, RDS, S3, Lambda and API Gateway. Managed EKS alone
costs roughly US$73/month for the control plane before any worker nodes. This project has a
zero budget.

## Decision

Every AWS dependency has a local, free substitute, selected so the *code* stays identical:

| AWS | Local | Same code path? |
|---|---|---|
| EKS | k3d (k3s in Docker) | Yes — real Kubernetes, same manifests |
| S3 | LocalStack Community | Yes — same AWS SDK, endpoint override only |
| Lambda | LocalStack Community | Yes — same handler, same S3 event shape |
| RDS Postgres | `postgres:16-alpine` | Yes — same Npgsql provider |
| API Gateway | Traefik Ingress + built-in rate limiter | Partly — different implementation, same behaviour |
| CloudWatch | stdout JSON via Serilog | Yes — CloudWatch ingests stdout on both ECS and EKS |

`AWS__ServiceUrl` is the only switch: set, the SDK targets LocalStack; empty, it uses the
default credential chain against real AWS.

Postgres replaces SQL Server despite the original brief. LocalStack Community does not emulate
RDS at all, the MSSQL image runs under emulation on Apple Silicon, and Npgsql is the
better-supported EF Core provider. The ORM code is unchanged either way.

## Consequences

The whole system is demonstrable on a laptop for free, and CI validates the Terraform and
Kubernetes code on every push.

What this does not prove: IRSA, the AWS Load Balancer Controller, EBS CSI volumes, RDS failover,
and real API Gateway behaviour. `README.md` states this plainly rather than implying the stack
has run in production.
