# Discovery ingestion — the observation contract

The discovery contract is the public, machine-checkable **wire format a scanner emits into
Atlas**: a batch of raw observations of an IT landscape (servers, applications,
infrastructure) that the **private** Atlas Enterprise reconciliation engine correlates,
deduplicates and turns into catalogue assets. Publishing the *format* lets third parties
build their own scanners and keeps the customer's data portable; the **reconciliation logic
that consumes it is the moat and is not part of this repo** (handbook `11 §3`, `12 §Phase A`;
see `vev-software/atlas-enterprise#6`/`#9`).

| Direction | Schema | SDK type | Purpose |
|---|---|---|---|
| Ingest (into Atlas) | [`discovery-observation.schema.json`](../schemas/v1/discovery-observation.schema.json) | `DiscoveryObservationBatch` / `DiscoveryObservationBatch` (TS) | A batch of raw sightings for the reconciler to turn into assets. |

## Facts only — what an observation is *not*

This is the boundary that protects the moat, and the schema enforces it:

- **No Atlas `id`.** An observation carries a scanner-local `observedId`; the reconciler
  decides catalogue identity. Asserting an Atlas `id` is rejected.
- **No `lifecycle`.** `draft`/`active`/`retired` is catalogue state the reconciler assigns,
  not a fact a scanner sees. Carrying it is rejected.
- **No analysis.** No criticality, EOL/risk, portfolio scoring or AI review — those *work
  with* the data and live in the private Atlas core (handbook `11 §1`). Any unknown property
  is rejected (`unevaluatedProperties: false`).

## The batch

```jsonc
{
  "contractVersion": "1",
  "kind": "discovery-observation",
  "source": { "agentId": "agent-eu-north-1-01", "method": "agent", "version": "0.1.0" },
  "observedAt": "2026-08-10T07:30:00Z",
  "observations": [ /* Observation[] */ ]
}
```

- `source` — which scanner produced the batch and how (`agent` installed in the landscape,
  or `agentless` remote scanning). `tenantRef` is an optional, **non-authoritative** hint:
  ingestion binds tenancy from the authenticated machine-principal (a Fabric concern), never
  from the payload.
- `observedAt` — when the scan was taken. `observations` may be empty (a scan that saw
  nothing new is still a valid batch).

## An observation

```jsonc
{
  "observedId": "host:5f3c9a2e-checkout-prod-01",
  "kind": "server",
  "name": "checkout-prod-01",
  "fingerprint": [
    { "key": "machine-id", "value": "5f3c9a2e…" },
    { "key": "cloud-instance-id", "value": "i-0abcd…" }
  ],
  "server": { "hostname": "checkout-prod-01.vev.internal", "operatingSystem": "Ubuntu 24.04 LTS" },
  "ports":    [ { "port": 443, "protocol": "tcp" } ],
  "processes":[ { "name": "nginx", "port": 443 } ],
  "packages": [ { "name": "openssl", "version": "3.0.13" } ]
}
```

- **`kind`** reuses the catalogue vocabulary (`system` / `application` / `server` /
  `infrastructure`), and the kind-specific held facts reuse `ApplicationDetails`,
  `ServerDetails`, `InfrastructureDetails` from `common.schema.json` — the observation maps
  onto the same asset model the reconciler writes to.
- **`fingerprint`** is the set of durable attributes a reconciler *may* correlate on —
  deliberately more than a hostname (machine-id, MAC, serial, cloud instance id). Both `key`
  and `value` are required (unlike a `Tag`). How attributes are weighted for matching is
  private reconciler logic, **not** part of this contract.
- **`ports` / `processes` / `packages`** are discovery-only observed facts — a recorded
  sighting, never a measured integration or an EOL assessment.

## Validate

Run the conformance kit (`dotnet test`) or validate a payload against
`schemas/v1/discovery-observation.schema.json` with any JSON Schema 2020-12 validator. The
kit proves the schema accepts a valid batch and the SDK-produced shape, that a batch
round-trips through the .NET SDK, and that the boundary rejections above hold.
