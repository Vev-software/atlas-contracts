# ADR 0001: Separate landscape share digest v2

Status: proposed for contract review; stage policy follows the owner decision in
[issue #45](https://github.com/Vev-software/atlas-contracts/issues/45).

## Context

V1 carries minimized items and integration counts, but no topology. Relations
require references unique within a single digest and a signed default-off scope.
Adding required refs to v1 would invalidate existing producers; expanding a closed
schema in place would change validation behavior for existing readers.

## Decision

Keep the v1 schema and SDK types unchanged. Add a separate digest schema under
`schemas/v2/`, literal `contractVersion: "2"`, and separately named .NET and
TypeScript types. Reuse unchanged v1 field definitions through relative schema
refs and ship both schema directories in both SDK packages. This does not change
the schema version of portability, bundle or other catalogue contracts.

Require per-digest opaque refs; never catalogue ids. Add optional layer hints and
closed edges carrying only `from`, `to`, `kind`, capped at 20,000. Schema checks
are supplemented by semantic validation of scope, refs, duplicates and endpoints.
`integrates` is undirected; reversed endpoints count as a duplicate.

Stage 1 covers `runsOn`, `suppliedBy` and `layer`. Define `integrates` now as
contract-only stage 2: the consuming product rejects it until a separate explicit
ADR addendum and consent allow it. A signed scope is sender intent, never trusted
authorization; the separate Fabric relations grant is enforced by the products.

## Consequences and migration

V2 readers accept v1 and v2 by selecting the corresponding schema and rules.
V1 producers continue unchanged with no deprecation deadline. V1 readers require
an update before v2 is delivered; producers must not silently downgrade a signed
v2 payload. Refs cannot be persisted as cross-digest identity.

The same detached RSA signature and replay protocol cover both versions, with
all v2 fields included under JCS. Publication to both public feeds is required
before an exporter pins v2. This decision adds no exporter or consumer code and
authorizes no merge, release, publication, or stage 2 enablement.
