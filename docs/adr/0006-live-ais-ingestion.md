# ADR 0006 — Live AIS ingestion, and what is real

**Status:** Accepted · 2026-07-30

## Context

The dashboard originally ran entirely on a telemetry simulator. That is defensible — nobody
building a portfolio has a fleet — but every number on screen was invented, which limits what
the anomaly rules actually prove.

AIS (Automatic Identification System) is broadcast in the clear by every commercial vessel
over 300 GT. [aisstream.io](https://aisstream.io) relays it over a WebSocket, free, with no
paid tier and no credit card.

## Decision

Ingest live AIS as an **opt-in** telemetry source. Exactly one source runs at a time:

- `Ais:Enabled=true` **and** a non-empty `Ais:ApiKey` → `AisIngestionService`
- otherwise → `TelemetrySimulatorService`

A fresh clone therefore works with no credentials, and the hosted demo never depends on a
third-party beta service being up.

## What is real and what is not

| Field | Source |
|---|---|
| Latitude, longitude | **Live AIS** |
| Speed over ground | **Live AIS** |
| Course, true heading, rate of turn | **Live AIS** |
| Navigational status → vessel status | **Live AIS** |
| Vessel name, IMO, call sign, type, dimensions | **Live AIS** (`ShipStaticData`) |
| Gross tonnage | **Estimated** from broadcast dimensions — AIS does not carry it |
| Engine temperature, shaft RPM, fuel flow | **Derived from speed** — see below |

Engine telemetry does not exist in AIS. It comes from proprietary onboard systems that are
not broadcast and are not publicly available from any source, free or paid. Rather than
pretend otherwise, `DerivedEngineMetrics` models it from speed over ground using a hotel load
plus a cubic resistance term, because hull resistance rises roughly with the cube of speed.
That is why fuel burn per nautical mile is high at low speed, bottoms out near economical
cruise, and climbs again when pressing on — the curve real operators optimise against.

Two of the five anomaly rules (engine overheat, fuel consumption) therefore run on modelled
inputs even in AIS mode. Three (position jump, implausible speed, sensor dropout) run on
genuinely real data.

## Consequences

**The architecture was already right.** aisstream.io explicitly does not support browser
connections: API keys should not reach clients, and connections are throttled per key. Their
recommended pattern is to consume the socket server-side and relay to clients over a
connection you control — which is precisely the existing ingestion → SignalR path. No
structural change was needed.

**Real data is messy, and that is the point.** Live AIS carries transponders with zeroed or
malformed IMO numbers, aids to navigation and coast stations broadcasting on ship-like MMSIs,
and sentinel values (speed 102.3, position 91/181, heading 511) meaning "not available".
`ImoNumber`, `MmsiNumber` and `AisSentinels` reject all of it at the boundary. A vessel that
cannot be identified is not admitted to the fleet.

**MMSI is the join key, IMO is the identity.** AIS position reports carry only the MMSI, so
`Vessel` gained a nullable `MmsiNumber`. It is nullable and not the primary identity on
purpose: an MMSI belongs to the radio installation and changes when a ship re-flags, whereas
the IMO number is assigned to the hull for life.

**Position reports for unknown ships are dropped.** A vessel is registered only after a
`ShipStaticData` message supplies a valid IMO number and a name. Registering from a position
report alone would create nameless, unidentifiable hulls.

**Volume is bounded.** Subscribed globally the feed averages ~300 messages/second and the
service disconnects clients that fall behind. The default subscription is a bounding box over
the approaches to Halifax, Nova Scotia, capped at 40 tracked vessels, with per-vessel storage
throttled to one reading every 30 seconds.

**The feed is beta with no SLA.** Reconnection uses exponential backoff capped at a minute,
and a malformed payload is logged and discarded rather than killing the read loop.
